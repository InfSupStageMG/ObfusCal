using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Persistence;
using BusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Infrastructure.Calendars;

public sealed class GoogleCalendarSourceCore(
    HttpClient httpClient,
    AppDbContext dbContext,
    ICalendarSourceInstanceStore calendarSourceInstanceStore,
    ICalendarSourceSecretProtector secretProtector,
    IGoogleOAuthTokenClient tokenClient,
    IOptions<GoogleConsentOptions> googleConsentOptions,
    ILogger<GoogleCalendarSourceCore> logger)
{
    private const string GooglePluginId = "google";
    private const string ManagedPropertyKey = "ObfusCal.Managed";
    private const string SlotIdPropertyKey = "ObfusCal.SlotId";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? calendarOwnerId = null,
        CancellationToken ct = default)
    {
        if (from > to)
            throw new ArgumentException("The start of the query window must be before the end.", nameof(from));

        ct.ThrowIfCancellationRequested();

        if (calendarOwnerId is null)
            return [];

        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(owner => owner.Id == calendarOwnerId.Value, ct);

        if (!ownerExists)
            return [];

        var instance = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId.Value, GooglePluginId, ct);
        if (instance is null)
            return [];

        return await GetEventsAsync(instance, from, to, ct);
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarSourceInstanceContext instance,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (from > to)
            throw new ArgumentException("The start of the query window must be before the end.", nameof(from));

        ct.ThrowIfCancellationRequested();

        if (!IsGoogleSource(instance))
            return [];

        var queryContext = await BuildQueryContextAsync(instance, ct);
        if (queryContext is null)
            return [];

        using var response = await QueryEventsWithRetryAsync(
            instance,
            queryContext.CalendarId,
            queryContext.SecretData,
            queryContext.AccessToken,
            from,
            to,
            ct);

        if (!IsSuccessfulResponse(instance, response))
            return [];

        return await ReadAndMapEventsAsync(response, from, to, ct);
    }

    private static bool IsGoogleSource(CalendarSourceInstanceContext instance)
        => string.Equals(instance.PluginId, GooglePluginId, StringComparison.OrdinalIgnoreCase);

    private async Task<GoogleQueryContext?> BuildQueryContextAsync(CalendarSourceInstanceContext instance, CancellationToken ct)
    {
        var configuration = ParseConfiguration(instance.ConfigurationJson) ?? new GoogleSourceConfiguration("primary");
        var secretData = ParseSecretData(instance.SecretDataJson);
        if (secretData is null || string.IsNullOrWhiteSpace(secretData.ProtectedAccessToken))
            return null;

        if (!TryUnprotectAccessToken(instance, secretData, out var accessToken))
            return null;

        accessToken = await RefreshIfExpiringAsync(instance, secretData, accessToken!, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var calendarId = string.IsNullOrWhiteSpace(configuration.CalendarId) ? "primary" : configuration.CalendarId;
        return new GoogleQueryContext(secretData, accessToken, calendarId);
    }

    private bool TryUnprotectAccessToken(
        CalendarSourceInstanceContext instance,
        GoogleSourceSecretData secretData,
        out string? accessToken)
    {
        try
        {
            accessToken = secretProtector.Unprotect(secretData.ProtectedAccessToken!);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Unable to read Google access token for calendar source instance {CalendarSourceInstanceId}; returning no events.",
                instance.Id);
            accessToken = null;
            return false;
        }
    }

    private bool IsSuccessfulResponse(CalendarSourceInstanceContext instance, HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogWarning(
                "Google Calendar authentication failed for calendar source instance {CalendarSourceInstanceId}.",
                instance.Id);
            return false;
        }

        if (response.IsSuccessStatusCode)
            return true;

        logger.LogWarning(
            "Google Calendar query failed for calendar source instance {CalendarSourceInstanceId} with HTTP {StatusCode}.",
            instance.Id,
            (int)response.StatusCode);
        return false;
    }

    private static async Task<IReadOnlyList<CalendarEvent>> ReadAndMapEventsAsync(
        HttpResponseMessage response,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var payload = await response.Content.ReadFromJsonAsync<GoogleCalendarEventsResponse>(JsonOptions, ct)
            ?? new GoogleCalendarEventsResponse(null, []);

        return payload.Items
            .Where(source => !IsManagedEvent(source))
            .Select(MapEvent)
            .Where(mapped => mapped is not null)
            .Select(mapped => mapped!)
            .Where(e => e.Start < to && e.End > from)
            .OrderBy(e => e.Start)
            .ToList();
    }

    private static bool IsManagedEvent(GoogleCalendarEvent source)
        => source.ExtendedProperties?.Private?.TryGetValue(ManagedPropertyKey, out var value) == true
           && string.Equals(value, "1", StringComparison.Ordinal);

    public async Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(owner => owner.Id == calendarOwnerId, ct);

        if (!ownerExists)
            return CalendarSourceReadiness.NotReady("Calendar owner not found.");

        var instance = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId, GooglePluginId, ct);
        if (instance is null)
        {
            return CalendarSourceReadiness.NotReady(
                "Google consent required.",
                "Complete Google consent for this calendar owner before requesting busy slots.");
        }

        return await GetReadinessAsync(instance, ct);
    }

    public static Task<CalendarSourceReadiness> GetReadinessAsync(CalendarSourceInstanceContext instance, CancellationToken ct = default)
    {
        var secretData = ParseSecretData(instance.SecretDataJson);
        var hasConsent = !string.IsNullOrWhiteSpace(secretData?.ProtectedAccessToken)
                         || !string.IsNullOrWhiteSpace(secretData?.ProtectedRefreshToken);

        return Task.FromResult(hasConsent
            ? CalendarSourceReadiness.Ready("Google Calendar is configured.")
            : CalendarSourceReadiness.NotReady(
                "Google consent required.",
                "Complete Google consent for this source instance before requesting busy slots."));
    }

    public async Task WriteBackSlotsAsync(
        Guid calendarOwnerId,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(owner => owner.Id == calendarOwnerId, ct);

        if (!ownerExists)
            return;

        var instance = (await calendarSourceInstanceStore.ListAsync(calendarOwnerId, ct))
            .FirstOrDefault(sourceInstance => sourceInstance.IsEnabled && IsGoogleSource(sourceInstance));

        if (instance is null)
            return;

        await WriteBackSlotsAsync(instance, busySlots, placeholderTitle, windowStart, windowEnd, ct);
    }

    public async Task WriteBackSlotsAsync(
        CalendarSourceInstanceContext instance,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        if (!IsGoogleSource(instance))
            return;

        var queryContext = await BuildQueryContextAsync(instance, ct);
        if (queryContext is null)
        {
            logger.LogWarning(
                "Write-back skipped for calendar source instance {CalendarSourceInstanceId}: no valid Google access token.",
                instance.Id);
            return;
        }

        var managedBySlotId = await GetManagedEventsBySlotIdAsync(
            instance,
            queryContext,
            windowStart,
            windowEnd,
            ct);
        var activeSlotIds = busySlots
            .Select(slot => slot.SourceEventId)
            .ToHashSet(StringComparer.Ordinal);

        await UpsertPlaceholderEventsAsync(instance, queryContext, busySlots, placeholderTitle, managedBySlotId, ct);
        var staleCount = await DeleteStaleManagedEventsAsync(
            instance,
            queryContext,
            managedBySlotId,
            activeSlotIds,
            windowStart,
            windowEnd,
            ct);

        logger.LogInformation(
            "Write-back complete for Google calendar source instance {CalendarSourceInstanceId}: {UpsertCount} active placeholder(s), {DeleteCount} stale placeholder(s) removed.",
            instance.Id,
            busySlots.Count,
            staleCount);
    }

    private async Task<Dictionary<string, ManagedGoogleEventRecord>> GetManagedEventsBySlotIdAsync(
        CalendarSourceInstanceContext instance,
        GoogleQueryContext queryContext,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var managedEvents = await GetManagedEventsAsync(
            instance,
            queryContext.CalendarId,
            queryContext.SecretData,
            queryContext.AccessToken,
            windowStart,
            windowEnd,
            ct);

        return managedEvents
            .Where(e => e.GoogleId is not null && e.SlotId is not null)
            .ToDictionary(e => e.SlotId!, e => e, StringComparer.Ordinal);
    }

    private async Task UpsertPlaceholderEventsAsync(
        CalendarSourceInstanceContext instance,
        GoogleQueryContext queryContext,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        IReadOnlyDictionary<string, ManagedGoogleEventRecord> managedBySlotId,
        CancellationToken ct)
    {
        foreach (var slot in busySlots)
        {
            if (managedBySlotId.TryGetValue(slot.SourceEventId, out var existing))
            {
                await UpdatePlaceholderEventIfNeededAsync(instance, queryContext, existing, slot, placeholderTitle, ct);
                continue;
            }

            await CreatePlaceholderEventAsync(
                instance,
                queryContext.CalendarId,
                queryContext.SecretData,
                queryContext.AccessToken,
                slot,
                placeholderTitle,
                ct);
        }
    }

    private async Task UpdatePlaceholderEventIfNeededAsync(
        CalendarSourceInstanceContext instance,
        GoogleQueryContext queryContext,
        ManagedGoogleEventRecord existing,
        BusySlot slot,
        string placeholderTitle,
        CancellationToken ct)
    {
        if (existing.Start == slot.Start
            && existing.End == slot.End
            && string.Equals(existing.Summary, placeholderTitle, StringComparison.Ordinal))
        {
            return;
        }

        await PatchPlaceholderEventAsync(
            instance,
            queryContext.CalendarId,
            queryContext.SecretData,
            queryContext.AccessToken,
            existing.GoogleId!,
            slot,
            placeholderTitle,
            ct);
    }

    private async Task<int> DeleteStaleManagedEventsAsync(
        CalendarSourceInstanceContext instance,
        GoogleQueryContext queryContext,
        IReadOnlyDictionary<string, ManagedGoogleEventRecord> managedBySlotId,
        IReadOnlySet<string> activeSlotIds,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var staleCount = 0;

        foreach (var (slotId, managedEvent) in managedBySlotId)
        {
            if (activeSlotIds.Contains(slotId) || managedEvent.Start < windowStart || managedEvent.Start >= windowEnd)
                continue;

            await DeleteEventAsync(
                instance,
                queryContext.CalendarId,
                queryContext.SecretData,
                queryContext.AccessToken,
                managedEvent.GoogleId!,
                ct);
            staleCount++;
        }

        return staleCount;
    }

    private async Task<string> RefreshIfExpiringAsync(
        CalendarSourceInstanceContext instance,
        GoogleSourceSecretData secretData,
        string accessToken,
        CancellationToken ct)
    {
        var expiresAt = secretData.TokenExpiresAtUtc;
        if (expiresAt is null || expiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(1))
            return accessToken;

        return await ForceRefreshAsync(instance, secretData, ct);
    }

    private async Task<string> ForceRefreshAsync(
        CalendarSourceInstanceContext instance,
        GoogleSourceSecretData secretData,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secretData.ProtectedRefreshToken))
        {
            logger.LogWarning(
                "Google access token refresh skipped for calendar source instance {CalendarSourceInstanceId}: no refresh token available.",
                instance.Id);
            return string.Empty;
        }

        string refreshToken;
        try
        {
            refreshToken = secretProtector.Unprotect(secretData.ProtectedRefreshToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Google access token refresh failed for calendar source instance {CalendarSourceInstanceId}: refresh token could not be read.",
                instance.Id);
            return string.Empty;
        }

        try
        {
            var refreshed = await tokenClient.RefreshAccessTokenAsync(refreshToken, ct);
            var updatedSecretData = secretData with
            {
                ProtectedAccessToken = secretProtector.Protect(refreshed.AccessToken),
                ProtectedRefreshToken = string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                    ? secretData.ProtectedRefreshToken
                    : secretProtector.Protect(refreshed.RefreshToken),
                TokenExpiresAtUtc = refreshed.ExpiresAtUtc,
                TokenLastRefreshedAtUtc = DateTimeOffset.UtcNow
            };

            await calendarSourceInstanceStore.UpdateSecretDataAsync(
                instance.CalendarOwnerId,
                instance.Id,
                JsonSerializer.Serialize(updatedSecretData),
                ct);

            return refreshed.AccessToken;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Google access token refresh failed for calendar source instance {CalendarSourceInstanceId}; returning no events.",
                instance.Id);
            return string.Empty;
        }
    }

    private async Task<IReadOnlyList<ManagedGoogleEventRecord>> GetManagedEventsAsync(
        CalendarSourceInstanceContext instance,
        string calendarId,
        GoogleSourceSecretData secretData,
        string accessToken,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var events = new List<ManagedGoogleEventRecord>();
        string? nextPageToken = null;

        do
        {
            using var response = await SendWithRetryAsync(
                instance,
                secretData,
                accessToken,
                token => CreateManagedEventsRequest(token, calendarId, windowStart, windowEnd, nextPageToken),
                ct);

            if (!IsSuccessfulResponse(instance, response))
                return [];

            var payload = await response.Content.ReadFromJsonAsync<GoogleCalendarEventsResponse>(JsonOptions, ct)
                ?? new GoogleCalendarEventsResponse(null, []);

            events.AddRange(payload.Items
                .Select(item =>
                {
                    var slotId = item.ExtendedProperties?.Private?.TryGetValue(SlotIdPropertyKey, out var value) == true
                        ? value
                        : null;
                    TryParseGoogleEventDate(item.Start, false, out var start);
                    TryParseGoogleEventDate(item.End, true, out var end);
                    return new ManagedGoogleEventRecord(item.Id, slotId, item.Summary, start, end);
                })
                .Where(item => item.GoogleId is not null));

            nextPageToken = payload.NextPageToken;
        } while (!string.IsNullOrWhiteSpace(nextPageToken));

        return events;
    }

    private async Task<HttpResponseMessage> QueryEventsWithRetryAsync(
        CalendarSourceInstanceContext instance,
        string calendarId,
        GoogleSourceSecretData secretData,
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        return await SendWithRetryAsync(
            instance,
            secretData,
            accessToken,
            token => CreateEventsQueryRequest(token, calendarId, from, to),
            ct);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        CalendarSourceInstanceContext instance,
        GoogleSourceSecretData secretData,
        string accessToken,
        Func<string, HttpRequestMessage> requestFactory,
        CancellationToken ct)
    {
        using var request = requestFactory(accessToken);
        var response = await httpClient.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();

        var refreshedAccessToken = await ForceRefreshAsync(instance, secretData, ct);
        if (string.IsNullOrWhiteSpace(refreshedAccessToken))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        using var retryRequest = requestFactory(refreshedAccessToken);
        return await httpClient.SendAsync(retryRequest, ct);
    }

    private async Task CreatePlaceholderEventAsync(
        CalendarSourceInstanceContext instance,
        string calendarId,
        GoogleSourceSecretData secretData,
        string accessToken,
        BusySlot slot,
        string placeholderTitle,
        CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(
            instance,
            secretData,
            accessToken,
            token => CreateGoogleWriteRequest(HttpMethod.Post, token, calendarId, null, slot, placeholderTitle, includeSlotMetadata: true),
            ct);

        if (response.IsSuccessStatusCode)
            return;

        logger.LogWarning(
            "Failed to create placeholder event for slot {SlotId} for calendar source instance {CalendarSourceInstanceId}: HTTP {StatusCode}.",
            slot.SourceEventId,
            instance.Id,
            (int)response.StatusCode);
    }

    private async Task PatchPlaceholderEventAsync(
        CalendarSourceInstanceContext instance,
        string calendarId,
        GoogleSourceSecretData secretData,
        string accessToken,
        string googleEventId,
        BusySlot slot,
        string placeholderTitle,
        CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(
            instance,
            secretData,
            accessToken,
            token => CreateGoogleWriteRequest(new HttpMethod("PATCH"), token, calendarId, googleEventId, slot, placeholderTitle, includeSlotMetadata: false),
            ct);

        if (response.IsSuccessStatusCode)
            return;

        logger.LogWarning(
            "Failed to patch placeholder event {GoogleEventId} for calendar source instance {CalendarSourceInstanceId}: HTTP {StatusCode}.",
            googleEventId,
            instance.Id,
            (int)response.StatusCode);
    }

    private async Task DeleteEventAsync(
        CalendarSourceInstanceContext instance,
        string calendarId,
        GoogleSourceSecretData secretData,
        string accessToken,
        string googleEventId,
        CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(
            instance,
            secretData,
            accessToken,
            token => CreateDeleteRequest(token, calendarId, googleEventId),
            ct);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            return;

        logger.LogWarning(
            "Failed to delete stale placeholder event {GoogleEventId} for calendar source instance {CalendarSourceInstanceId}: HTTP {StatusCode}.",
            googleEventId,
            instance.Id,
            (int)response.StatusCode);
    }

    private HttpRequestMessage CreateEventsQueryRequest(
        string accessToken,
        string calendarId,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var requestUri =
            $"{GetGoogleApiBaseUrl().TrimEnd('/')}/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events" +
            $"?singleEvents=true&orderBy=startTime&timeMin={Uri.EscapeDataString(from.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&timeMax={Uri.EscapeDataString(to.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}";

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private HttpRequestMessage CreateManagedEventsRequest(
        string accessToken,
        string calendarId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? pageToken)
    {
        var requestUri =
            $"{GetGoogleApiBaseUrl().TrimEnd('/')}/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events" +
            $"?singleEvents=true&orderBy=startTime&timeMin={Uri.EscapeDataString(from.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&timeMax={Uri.EscapeDataString(to.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&privateExtendedProperty={Uri.EscapeDataString($"{ManagedPropertyKey}=1")}";

        if (!string.IsNullOrWhiteSpace(pageToken))
            requestUri += $"&pageToken={Uri.EscapeDataString(pageToken)}";

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private HttpRequestMessage CreateGoogleWriteRequest(
        HttpMethod method,
        string accessToken,
        string calendarId,
        string? googleEventId,
        BusySlot slot,
        string placeholderTitle,
        bool includeSlotMetadata)
    {
        var requestUri = string.IsNullOrWhiteSpace(googleEventId)
            ? $"{GetGoogleApiBaseUrl().TrimEnd('/')}/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events"
            : $"{GetGoogleApiBaseUrl().TrimEnd('/')}/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(googleEventId)}";

        object body = includeSlotMetadata
            ? new
            {
                summary = placeholderTitle,
                start = new { dateTime = slot.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) },
                end = new { dateTime = slot.End.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) },
                transparency = "opaque",
                reminders = new { useDefault = false },
                extendedProperties = new
                {
                    @private = new Dictionary<string, string>
                    {
                        [ManagedPropertyKey] = "1",
                        [SlotIdPropertyKey] = slot.SourceEventId
                    }
                }
            }
            : new
            {
                summary = placeholderTitle,
                start = new { dateTime = slot.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) },
                end = new { dateTime = slot.End.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) },
                transparency = "opaque",
                reminders = new { useDefault = false }
            };

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return request;
    }

    private HttpRequestMessage CreateDeleteRequest(
        string accessToken,
        string calendarId,
        string googleEventId)
    {
        var requestUri =
            $"{GetGoogleApiBaseUrl().TrimEnd('/')}/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(googleEventId)}";

        var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private string GetGoogleApiBaseUrl()
    {
        var baseUrl = googleConsentOptions.Value.ApiBaseUrl.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("GoogleConsent:ApiBaseUrl is required.");

        return baseUrl;
    }

    private static CalendarEvent? MapEvent(GoogleCalendarEvent source)
    {
        if (!TryParseGoogleEventDate(source.Start, false, out var start)
            || !TryParseGoogleEventDate(source.End, true, out var end)
            || end <= start)
        {
            return null;
        }

        var attendees = source.Attendees?
            .Select(attendee => attendee.Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Cast<string>()
            .ToArray() ?? [];

        var id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id;
        var title = string.IsNullOrWhiteSpace(source.Summary) ? "Busy" : source.Summary;

        return new CalendarEvent(
            id,
            title,
            source.Description,
            start,
            end,
            attendees,
            source.Location);
    }

    private static bool TryParseGoogleEventDate(GoogleEventDate? source, bool isEnd, out DateTimeOffset value)
    {
        value = default;
        if (source is null)
            return false;

        if (!string.IsNullOrWhiteSpace(source.DateTime)
            && DateTimeOffset.TryParse(source.DateTime, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDateTime))
        {
            value = parsedDateTime;
            return true;
        }

        if (string.IsNullOrWhiteSpace(source.Date)
            || !DateOnly.TryParseExact(source.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dateOnly))
        {
            return false;
        }

        var dateTime = dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        value = new DateTimeOffset(dateTime, TimeSpan.Zero);
        if (isEnd)
            value = value.AddDays(1);

        return true;
    }

    private static GoogleSourceConfiguration? ParseConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GoogleSourceConfiguration>(configurationJson);
        }
        catch
        {
            return null;
        }
    }

    private static GoogleSourceSecretData? ParseSecretData(string? secretDataJson)
    {
        if (string.IsNullOrWhiteSpace(secretDataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GoogleSourceSecretData>(secretDataJson);
        }
        catch
        {
            return null;
        }
    }

    private sealed record GoogleCalendarEventsResponse(
        [property: JsonPropertyName("nextPageToken")]
        string? NextPageToken,
        [property: JsonPropertyName("items")]
        List<GoogleCalendarEvent> Items);

    private sealed record GoogleCalendarEvent(
        [property: JsonPropertyName("id")]
        string? Id,
        [property: JsonPropertyName("summary")]
        string? Summary,
        [property: JsonPropertyName("description")]
        string? Description,
        [property: JsonPropertyName("start")]
        GoogleEventDate? Start,
        [property: JsonPropertyName("end")]
        GoogleEventDate? End,
        [property: JsonPropertyName("attendees")]
        List<GoogleEventAttendee>? Attendees,
        [property: JsonPropertyName("extendedProperties")]
        GoogleEventExtendedProperties? ExtendedProperties,
        [property: JsonPropertyName("location")]
        string? Location);

    private sealed record GoogleEventExtendedProperties(
        [property: JsonPropertyName("private")]
        Dictionary<string, string>? Private);

    private sealed record GoogleEventDate(
        [property: JsonPropertyName("dateTime")]
        string? DateTime,
        [property: JsonPropertyName("date")]
        string? Date);

    private sealed record GoogleEventAttendee(
        [property: JsonPropertyName("email")]
        string? Email);

    private sealed record GoogleQueryContext(
        GoogleSourceSecretData SecretData,
        string AccessToken,
        string CalendarId);

    private sealed record ManagedGoogleEventRecord(
        string? GoogleId,
        string? SlotId,
        string? Summary,
        DateTimeOffset Start,
        DateTimeOffset End);

    internal sealed record GoogleSourceConfiguration(string? CalendarId);

    internal sealed record GoogleSourceSecretData(
        string? ProtectedAccessToken,
        string? ProtectedRefreshToken,
        DateTimeOffset? ConsentGrantedAtUtc,
        DateTimeOffset? TokenExpiresAtUtc,
        DateTimeOffset? TokenLastRefreshedAtUtc);
}

