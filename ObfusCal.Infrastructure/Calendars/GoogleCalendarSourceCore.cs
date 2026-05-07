using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Persistence;

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
            ?? new GoogleCalendarEventsResponse([]);

        return payload.Items
            .Select(MapEvent)
            .Where(mapped => mapped is not null)
            .Select(mapped => mapped!)
            .Where(e => e.Start < to && e.End > from)
            .OrderBy(e => e.Start)
            .ToList();
    }

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

    private async Task<HttpResponseMessage> QueryEventsWithRetryAsync(
        CalendarSourceInstanceContext instance,
        string calendarId,
        GoogleSourceSecretData secretData,
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var response = await QueryEventsAsync(calendarId, accessToken, from, to, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();

        var refreshedAccessToken = await ForceRefreshAsync(instance, secretData, ct);
        if (string.IsNullOrWhiteSpace(refreshedAccessToken))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        return await QueryEventsAsync(calendarId, refreshedAccessToken, from, to, ct);
    }

    private async Task<HttpResponseMessage> QueryEventsAsync(
        string calendarId,
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var baseUrl = googleConsentOptions.Value.ApiBaseUrl.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("GoogleConsent:ApiBaseUrl is required.");

        var requestUri =
            $"{baseUrl.TrimEnd('/')}/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events" +
            $"?singleEvents=true&orderBy=startTime&timeMin={Uri.EscapeDataString(from.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&timeMax={Uri.EscapeDataString(to.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");

        return await httpClient.SendAsync(request, ct);
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
        [property: JsonPropertyName("location")]
        string? Location);

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

    internal sealed record GoogleSourceConfiguration(string? CalendarId);

    internal sealed record GoogleSourceSecretData(
        string? ProtectedAccessToken,
        string? ProtectedRefreshToken,
        DateTimeOffset? ConsentGrantedAtUtc,
        DateTimeOffset? TokenExpiresAtUtc,
        DateTimeOffset? TokenLastRefreshedAtUtc);
}

