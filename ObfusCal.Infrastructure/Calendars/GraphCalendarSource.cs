using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using BusySlot = ObfusCal.Domain.Models.BusySlot;
using CalendarEvent = ObfusCal.Domain.Models.CalendarEvent;

namespace ObfusCal.Infrastructure.Calendars;

[CalendarSourcePlugin("graph", "Microsoft Graph")]
[CalendarSourcePluginUi(
    supportsMultipleInstances: true,
    configurationJsonTemplate: "{\"calendarId\":\"primary\"}",
    setupHint: "Use the Graph consent flow to populate tokens for each source instance.")]
[CalendarSourcePluginAction(
    "graph-instance-consent",
    "Start Microsoft OAuth",
    hint: "Authorizes ObfusCal to read your Microsoft Graph calendar and maintain ObfusCal-managed busy placeholders for this source instance.")]
public sealed class GraphCalendarSource(
    HttpClient httpClient,
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    IGraphOAuthTokenClient tokenClient,
    ICalendarSourceInstanceStore calendarSourceInstanceStore,
    ILogger<GraphCalendarSource> logger)
    : ICalendarSource, ICalendarWriteBack, ICalendarSourceInstanceWriteBack, ICalendarSourceReadinessEvaluator, ICalendarSourceInstanceHandler, ICalendarSourceInstanceReadinessEvaluator
{
    private const string GraphCalendarViewPath = "v1.0/me/calendarView";
    private const string GraphEventsPath = "v1.0/me/events";
    private const int GraphCalendarViewPageSize = 1000;
    private const string ObfusCalPropertyNamespace = "e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0";
    private const string ManagedPropertyId = "String {" + ObfusCalPropertyNamespace + "} Name ObfusCal.Managed";
    private const string SlotIdPropertyId  = "String {" + ObfusCalPropertyNamespace + "} Name ObfusCal.SlotId";

    private readonly IDataProtector _tokenProtector = dataProtectionProvider
        .CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

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

        var owner = await dbContext.CalendarOwners
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId.Value, ct);

        if (owner is null)
            return [];

        var accessToken = await GetOrRefreshOwnerAccessTokenAsync(owner, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
            return [];

        using var response = await GetCalendarViewWithRetryAsync(owner, accessToken, from, to, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Graph calendar fetch failed for calendar owner {CalendarOwnerId} with HTTP {StatusCode}.",
                calendarOwnerId.Value,
                (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphCalendarViewResponse>(cancellationToken: ct);
        var events = await CollectAllPagesAsync(payload, accessToken, ct);

        return events
            .Where(source => !IsManagedEvent(source))
            .Select(MapEvent)
            .Where(mapped => mapped is not null)
            .Select(mapped => mapped!)
            .Where(e => e.Start < to && e.End > from)
            .OrderBy(e => e.Start)
            .ToList();
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

        var secretData = ParseSecretData(instance.SecretDataJson);
        if (secretData is null || string.IsNullOrWhiteSpace(secretData.ProtectedAccessToken))
            return [];

        string accessToken;
        try
        {
            accessToken = _tokenProtector.Unprotect(secretData.ProtectedAccessToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Unable to read Graph access token for calendar source instance {CalendarSourceInstanceId}; returning no events.",
                instance.Id);
            return [];
        }

        accessToken = await RefreshIfExpiringAsync(instance, secretData, accessToken, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
            return [];

        using var response = await GetCalendarViewWithRetryAsync(instance, secretData, accessToken, from, to, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Graph calendar fetch failed for calendar source instance {CalendarSourceInstanceId} with HTTP {StatusCode}.",
                instance.Id,
                (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphCalendarViewResponse>(cancellationToken: ct);
        var events = await CollectAllPagesAsync(payload, accessToken, ct);

        return events
            .Where(source => !IsManagedEvent(source))
            .Select(MapEvent)
            .Where(mapped => mapped is not null)
            .Select(mapped => mapped!)
            .Where(e => e.Start < to && e.End > from)
            .OrderBy(e => e.Start)
            .ToList();
    }

    public async Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var owner = await dbContext.CalendarOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId, ct);

        if (owner is null)
            return CalendarSourceReadiness.NotReady("Calendar owner not found.");

        var hasConsent = !string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected)
            || !string.IsNullOrWhiteSpace(owner.GraphRefreshTokenProtected);

        return hasConsent
            ? CalendarSourceReadiness.Ready("Microsoft Graph calendar is configured.")
            : CalendarSourceReadiness.NotReady(
                "Microsoft Graph consent required.",
                "This calendar owner has not granted Microsoft Graph calendar consent yet. Complete consent before requesting busy slots.");
    }

    public Task<CalendarSourceReadiness> GetReadinessAsync(CalendarSourceInstanceContext instance, CancellationToken ct = default)
    {
        var secretData = ParseSecretData(instance.SecretDataJson);
        var hasConsent = !string.IsNullOrWhiteSpace(secretData?.ProtectedAccessToken)
            || !string.IsNullOrWhiteSpace(secretData?.ProtectedRefreshToken);

        return Task.FromResult(hasConsent
            ? CalendarSourceReadiness.Ready("Microsoft Graph calendar is configured.")
            : CalendarSourceReadiness.NotReady(
                "Microsoft Graph consent required.",
                "Complete Microsoft Graph consent for this source instance before requesting busy slots."));
    }

    public async Task WriteBackSlotsAsync(
        Guid calendarOwnerId,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        var owner = await dbContext.CalendarOwners
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId, ct);
        if (owner is null)
            return;

        var accessToken = await GetOrRefreshOwnerAccessTokenAsync(owner, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogWarning(
                "Write-back skipped for calendar owner {CalendarOwnerId}: no valid Graph access token.",
                calendarOwnerId);
            return;
        }

        await WriteBackSlotsCoreAsync(
            accessToken,
            busySlots,
            placeholderTitle,
            calendarOwnerId,
            windowStart,
            windowEnd,
            ct);
    }

    public async Task WriteBackSlotsAsync(
        CalendarSourceInstanceContext instance,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        var secretData = ParseSecretData(instance.SecretDataJson);
        if (secretData is null || string.IsNullOrWhiteSpace(secretData.ProtectedAccessToken))
        {
            logger.LogWarning(
                "Write-back skipped for calendar source instance {CalendarSourceInstanceId}: no valid Graph access token.",
                instance.Id);
            return;
        }

        string accessToken;
        try
        {
            accessToken = _tokenProtector.Unprotect(secretData.ProtectedAccessToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Unable to read Graph access token for calendar source instance {CalendarSourceInstanceId}; write-back skipped.",
                instance.Id);
            return;
        }

        accessToken = await RefreshIfExpiringAsync(instance, secretData, accessToken, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogWarning(
                "Write-back skipped for calendar source instance {CalendarSourceInstanceId}: no valid Graph access token.",
                instance.Id);
            return;
        }

        await WriteBackSlotsCoreAsync(
            accessToken,
            busySlots,
            placeholderTitle,
            instance.CalendarOwnerId,
            windowStart,
            windowEnd,
            ct);
    }

    private async Task WriteBackSlotsCoreAsync(
        string accessToken,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        Guid calendarOwnerId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var managedEvents = await GetManagedEventsAsync(accessToken, calendarOwnerId, windowStart, windowEnd, ct);

        var managedBySlotId = managedEvents
            .Where(e => e.GraphId is not null && e.SlotId is not null)
            .ToDictionary(e => e.SlotId!, e => e, StringComparer.Ordinal);

        var activeSlotIds = busySlots
            .Select(s => s.SourceEventId)
            .ToHashSet(StringComparer.Ordinal);

        // Upsert: create new or patch changed existing placeholder events.
        foreach (var slot in busySlots)
        {
            if (managedBySlotId.TryGetValue(slot.SourceEventId, out var existing))
            {
                if (existing.Start != slot.Start || existing.End != slot.End
                    || !string.Equals(existing.Subject, placeholderTitle, StringComparison.Ordinal))
                {
                    await PatchPlaceholderEventAsync(existing.GraphId!, accessToken, slot, placeholderTitle, calendarOwnerId, ct);
                }
            }
            else
            {
                await CreatePlaceholderEventAsync(accessToken, slot, placeholderTitle, calendarOwnerId, ct);
            }
        }

        // Cleanup: only delete stale managed events whose start falls within the write-back window.
        // Events beyond the window belong to shadow slots not yet in scope; deleting them here would
        // cause placeholder churn as the advancing window temporarily excludes future slots.
        var staleCount = 0;
        foreach (var (slotId, ev) in managedBySlotId)
        {
            if (activeSlotIds.Contains(slotId) || ev.Start < windowStart || ev.Start >= windowEnd) continue;
            await DeleteEventAsync(ev.GraphId!, accessToken, calendarOwnerId, ct);
            staleCount++;
        }

        logger.LogInformation(
            "Write-back complete for calendar owner {CalendarOwnerId}: {UpsertCount} active placeholder(s), {DeleteCount} stale placeholder(s) removed.",
            calendarOwnerId,
            busySlots.Count,
            staleCount);
    }

    private async Task<IReadOnlyList<ManagedEventRecord>> GetManagedEventsAsync(
        string accessToken,
        Guid calendarOwnerId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var managedFilter = $"singleValueExtendedProperties/Any(ep: ep/id eq '{ManagedPropertyId}' and ep/value eq '1')";
        var windowFilter =
            $"start/dateTime ge '{windowStart.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}' and start/dateTime lt '{windowEnd.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}'";
        var filter = $"{managedFilter} and {windowFilter}";
        var expand = $"singleValueExtendedProperties($filter=id eq '{SlotIdPropertyId}')";
        var requestUri =
            $"{GraphEventsPath}?$filter={Uri.EscapeDataString(filter)}&$expand={Uri.EscapeDataString(expand)}&$select=id,subject,start,end&$top={GraphCalendarViewPageSize}";

        var events = new List<ManagedEventRecord>();
        string? nextPageUri = requestUri;

        while (!string.IsNullOrWhiteSpace(nextPageUri))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextPageUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to fetch ObfusCal-managed Graph events for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
                    calendarOwnerId,
                    (int)response.StatusCode);
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<GraphManagedEventsResponse>(cancellationToken: ct);
            if (payload?.Value is not null)
            {
                events.AddRange(payload.Value
                    .Select(dto =>
                    {
                        var slotId = dto.ExtendedProperties?
                            .FirstOrDefault(p => string.Equals(p.Id, SlotIdPropertyId, StringComparison.Ordinal))
                            ?.Value;
                        TryParseGraphDateTime(dto.Start, out var start);
                        TryParseGraphDateTime(dto.End, out var end);
                        return new ManagedEventRecord(dto.Id, slotId, dto.Subject, start, end);
                    })
                    .Where(e => e.GraphId is not null));
            }

            nextPageUri = payload?.NextLink;
        }

        return events;
    }

    private async Task CreatePlaceholderEventAsync(
        string accessToken,
        BusySlot slot,
        string placeholderTitle,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        var body = new
        {
            subject = placeholderTitle,
            start = new { dateTime = slot.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" },
            end   = new { dateTime = slot.End.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),   timeZone = "UTC" },
            showAs = "busy",
            isReminderOn = false,
            singleValueExtendedProperties = new[]
            {
                new { id = ManagedPropertyId, value = "1" },
                new { id = SlotIdPropertyId,  value = slot.SourceEventId }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphEventsPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Failed to create placeholder event for slot {SlotId} for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
                slot.SourceEventId, calendarOwnerId, (int)response.StatusCode);
        }
    }

    private async Task PatchPlaceholderEventAsync(
        string graphEventId,
        string accessToken,
        BusySlot slot,
        string placeholderTitle,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        var body = new
        {
            subject = placeholderTitle,
            start = new { dateTime = slot.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" },
            end   = new { dateTime = slot.End.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),   timeZone = "UTC" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{GraphEventsPath}/{Uri.EscapeDataString(graphEventId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Failed to patch placeholder event {GraphEventId} for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
                graphEventId, calendarOwnerId, (int)response.StatusCode);
        }
    }

    private async Task DeleteEventAsync(
        string graphEventId,
        string accessToken,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{GraphEventsPath}/{Uri.EscapeDataString(graphEventId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            logger.LogWarning(
                "Failed to delete stale placeholder event {GraphEventId} for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
                graphEventId, calendarOwnerId, (int)response.StatusCode);
        }
    }

    private async Task<string?> GetOrRefreshOwnerAccessTokenAsync(CalendarOwner owner, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected))
            return null;

        string accessToken;
        try
        {
            accessToken = _tokenProtector.Unprotect(owner.GraphAccessTokenProtected);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Unable to read Graph access token for calendar owner {CalendarOwnerId}.",
                owner.Id);
            return null;
        }

        var refreshed = await RefreshIfExpiringAsync(owner, accessToken, ct);
        return string.IsNullOrWhiteSpace(refreshed) ? null : refreshed;
    }

    private async Task<string> RefreshIfExpiringAsync(CalendarOwner owner, string accessToken, CancellationToken ct)
    {
        var expiresAt = owner.GraphTokenExpiresAtUtc;
        if (expiresAt is null || expiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(1))
            return accessToken;

        return await ForceRefreshAsync(owner, ct);
    }

    private async Task<string> RefreshIfExpiringAsync(
        CalendarSourceInstanceContext instance,
        GraphSourceSecretData secretData,
        string accessToken,
        CancellationToken ct)
    {
        var expiresAt = secretData.TokenExpiresAtUtc;
        if (expiresAt is null || expiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(1))
            return accessToken;

        return await ForceRefreshAsync(instance, secretData, ct);
    }

    private async Task<string> ForceRefreshAsync(CalendarOwner owner, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(owner.GraphRefreshTokenProtected))
        {
            logger.LogWarning(
                "Graph access token refresh skipped for calendar owner {CalendarOwnerId}: no refresh token available.",
                owner.Id);
            return string.Empty;
        }

        string refreshToken;
        try
        {
            refreshToken = _tokenProtector.Unprotect(owner.GraphRefreshTokenProtected);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Graph access token refresh failed for calendar owner {CalendarOwnerId}: refresh token could not be read.",
                owner.Id);
            return string.Empty;
        }

        try
        {
            var refreshed = await tokenClient.RefreshAccessTokenAsync(refreshToken, ct);
            owner.GraphAccessTokenProtected = _tokenProtector.Protect(refreshed.AccessToken);
            if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                owner.GraphRefreshTokenProtected = _tokenProtector.Protect(refreshed.RefreshToken);

            owner.GraphTokenExpiresAtUtc = refreshed.ExpiresAtUtc;
            owner.GraphTokenLastRefreshedAtUtc = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(ct);
            return refreshed.AccessToken;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Graph access token refresh failed for calendar owner {CalendarOwnerId}; returning no events.",
                owner.Id);
            return string.Empty;
        }
    }

    private async Task<string> ForceRefreshAsync(
        CalendarSourceInstanceContext instance,
        GraphSourceSecretData secretData,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secretData.ProtectedRefreshToken))
        {
            logger.LogWarning(
                "Graph access token refresh skipped for calendar source instance {CalendarSourceInstanceId}: no refresh token available.",
                instance.Id);
            return string.Empty;
        }

        string refreshToken;
        try
        {
            refreshToken = _tokenProtector.Unprotect(secretData.ProtectedRefreshToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Graph access token refresh failed for calendar source instance {CalendarSourceInstanceId}: refresh token could not be read.",
                instance.Id);
            return string.Empty;
        }

        try
        {
            var refreshed = await tokenClient.RefreshAccessTokenAsync(refreshToken, ct);
            var updatedSecretData = secretData with
            {
                ProtectedAccessToken = _tokenProtector.Protect(refreshed.AccessToken),
                ProtectedRefreshToken = string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                    ? secretData.ProtectedRefreshToken
                    : _tokenProtector.Protect(refreshed.RefreshToken),
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
                "Graph access token refresh failed for calendar source instance {CalendarSourceInstanceId}; returning no events.",
                instance.Id);
            return string.Empty;
        }
    }

    private async Task<HttpResponseMessage> GetCalendarViewAsync(
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var expand = $"singleValueExtendedProperties($filter=id eq '{ManagedPropertyId}')";
        var requestUri =
            $"{GraphCalendarViewPath}?startDateTime={Uri.EscapeDataString(from.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}&endDateTime={Uri.EscapeDataString(to.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}&$expand={Uri.EscapeDataString(expand)}&$top={GraphCalendarViewPageSize}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");

        return await httpClient.SendAsync(request, ct);
    }

    /// <summary>Follows @odata.nextLink pages until exhausted and returns all events.</summary>
    private async Task<List<GraphEvent>> CollectAllPagesAsync(
        GraphCalendarViewResponse? firstPage,
        string accessToken,
        CancellationToken ct)
    {
        var allEvents = new List<GraphEvent>();
        var page = firstPage;
        var seenNextLinks = new HashSet<string>(StringComparer.Ordinal);

        while (page is not null)
        {
            allEvents.AddRange(page.Value ?? []);

            if (string.IsNullOrWhiteSpace(page.NextLink))
                break;

            if (!seenNextLinks.Add(page.NextLink))
            {
                logger.LogWarning(
                    "Graph calendarView pagination returned a repeated nextLink; stopping early to avoid an infinite loop.");
                break;
            }

            page = await FetchNextPageAsync(accessToken, page.NextLink, ct);
        }

        return allEvents;
    }

    private async Task<GraphCalendarViewResponse?> FetchNextPageAsync(
        string accessToken,
        string nextLink,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, nextLink);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Graph calendarView next-page fetch failed with HTTP {StatusCode}; pagination stopped early.",
                (int)response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<GraphCalendarViewResponse>(cancellationToken: ct);
    }

    private async Task<HttpResponseMessage> GetCalendarViewWithRetryAsync(
        CalendarOwner owner,
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var response = await GetCalendarViewAsync(accessToken, from, to, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();

        var refreshedAccessToken = await ForceRefreshAsync(owner, ct);
        if (string.IsNullOrWhiteSpace(refreshedAccessToken))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        return await GetCalendarViewAsync(refreshedAccessToken, from, to, ct);
    }

    private async Task<HttpResponseMessage> GetCalendarViewWithRetryAsync(
        CalendarSourceInstanceContext instance,
        GraphSourceSecretData secretData,
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var response = await GetCalendarViewAsync(accessToken, from, to, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();

        var refreshedAccessToken = await ForceRefreshAsync(instance, secretData, ct);
        if (string.IsNullOrWhiteSpace(refreshedAccessToken))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        return await GetCalendarViewAsync(refreshedAccessToken, from, to, ct);
    }

    private static GraphSourceSecretData? ParseSecretData(string? secretDataJson)
    {
        if (string.IsNullOrWhiteSpace(secretDataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GraphSourceSecretData>(secretDataJson);
        }
        catch
        {
            return null;
        }
    }

    private CalendarEvent? MapEvent(GraphEvent source)
    {
        if (!TryParseGraphDateTime(source.Start, out var start)
            || !TryParseGraphDateTime(source.End, out var end)
            || end <= start)
        {
            return null;
        }

        var attendees = source.Attendees?
            .Select(attendee => attendee.EmailAddress?.Address)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Cast<string>()
            .ToArray() ?? [];

        var id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id;
        var title = string.IsNullOrWhiteSpace(source.Subject) ? "Busy" : source.Subject;

        return new CalendarEvent(
            id,
            title,
            source.BodyPreview,
            start,
            end,
            attendees,
            source.Location?.DisplayName);
    }

    private static bool IsManagedEvent(GraphEvent source)
        => source.ExtendedProperties?.Any(property =>
               string.Equals(property.Id, ManagedPropertyId, StringComparison.Ordinal)
               && string.Equals(property.Value, "1", StringComparison.Ordinal))
           == true;

    private bool TryParseGraphDateTime(GraphDateTimeTimeZone? source, out DateTimeOffset value)
    {
        value = default;
        if (source is null || string.IsNullOrWhiteSpace(source.DateTime))
            return false;

        if (DateTimeOffset.TryParse(source.DateTime, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedOffset))
        {
            value = parsedOffset;
            return true;
        }

        if (!DateTime.TryParse(source.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsedDateTime))
            return false;

        var timeZoneId = source.TimeZone;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                value = TimeZoneInfo.ConvertTimeToUtc(parsedDateTime, timeZone);
                return true;
            }
            catch (TimeZoneNotFoundException ex)
            {
                logger.LogDebug(ex,
                    "Graph event timezone '{TimeZoneId}' was not found on this host. Falling back to UTC parsing.",
                    timeZoneId);
            }
            catch (InvalidTimeZoneException ex)
            {
                logger.LogDebug(ex,
                    "Graph event timezone '{TimeZoneId}' is invalid on this host. Falling back to UTC parsing.",
                    timeZoneId);
            }
        }

        value = new DateTimeOffset(DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc));
        return true;
    }

    private sealed record GraphCalendarViewResponse(
        [property: JsonPropertyName("value")] List<GraphEvent>? Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private sealed record GraphEvent(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("subject")]
        string? Subject,
        [property: JsonPropertyName("bodyPreview")]
        string? BodyPreview,
        [property: JsonPropertyName("start")] GraphDateTimeTimeZone? Start,
        [property: JsonPropertyName("end")] GraphDateTimeTimeZone? End,
        [property: JsonPropertyName("attendees")]
        List<GraphAttendee>? Attendees,
        [property: JsonPropertyName("singleValueExtendedProperties")]
        List<GraphExtendedProperty>? ExtendedProperties,
        [property: JsonPropertyName("location")]
        GraphLocation? Location);

    private sealed record GraphDateTimeTimeZone(
        [property: JsonPropertyName("dateTime")]
        string? DateTime,
        [property: JsonPropertyName("timeZone")]
        string? TimeZone);

    private sealed record GraphAttendee(
        [property: JsonPropertyName("emailAddress")]
        GraphEmailAddress? EmailAddress);

    private sealed record GraphEmailAddress(
        [property: JsonPropertyName("address")]
        string? Address);

    private sealed record GraphLocation(
        [property: JsonPropertyName("displayName")]
        string? DisplayName);

    // Write-back DTOs
    private sealed record GraphManagedEventsResponse(
        [property: JsonPropertyName("value")] List<GraphManagedEventDto>? Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private sealed record GraphManagedEventDto(
        [property: JsonPropertyName("id")]      string? Id,
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("start")]   GraphDateTimeTimeZone? Start,
        [property: JsonPropertyName("end")]     GraphDateTimeTimeZone? End,
        [property: JsonPropertyName("singleValueExtendedProperties")]
        List<GraphExtendedProperty>? ExtendedProperties);

    private sealed record GraphExtendedProperty(
        [property: JsonPropertyName("id")]    string? Id,
        [property: JsonPropertyName("value")] string? Value);

    private sealed record ManagedEventRecord(
        string? GraphId,
        string? SlotId,
        string? Subject,
        DateTimeOffset Start,
        DateTimeOffset End);

    internal sealed record GraphSourceSecretData(
        string? ProtectedAccessToken,
        string? ProtectedRefreshToken,
        DateTimeOffset? ConsentGrantedAtUtc,
        DateTimeOffset? TokenExpiresAtUtc,
        DateTimeOffset? TokenLastRefreshedAtUtc);
}
