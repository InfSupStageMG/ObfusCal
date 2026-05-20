using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using BusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Infrastructure.Calendars;

public sealed partial class GoogleCalendarSourceCore
{
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

        var bySlotId = new Dictionary<string, ManagedGoogleEventRecord>(StringComparer.Ordinal);
        foreach (var e in managedEvents.Where(e => e.GoogleId is not null && e.SlotId is not null))
        {
            if (bySlotId.TryAdd(e.SlotId!, e)) continue;
            logger.LogWarning(
                "Duplicate managed Google event found for SlotId {SlotId} in calendar source instance {CalendarSourceInstanceId}; removing the extra copy.",
                e.SlotId,
                instance.Id);
            await DeleteEventAsync(
                instance,
                queryContext.CalendarId,
                queryContext.SecretData,
                queryContext.AccessToken,
                e.GoogleId!,
                ct);
        }

        return bySlotId;
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
}


