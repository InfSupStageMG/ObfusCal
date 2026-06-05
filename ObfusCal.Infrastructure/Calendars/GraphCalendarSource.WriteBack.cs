using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Calendars;
using ObfusCal.Application.Interfaces;
using BusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Infrastructure.Calendars;

public sealed partial class GraphCalendarSource
{
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

        if (!GraphConsentAccessPolicy.AllowsOwnerWriteBack(owner.GraphGrantedScopes))
        {
            logger.LogInformation(
                "Write-back skipped for calendar owner {CalendarOwnerId}: Graph consent is read-only.",
                calendarOwnerId);
            return;
        }

        var tokenSession = await CreateOwnerTokenSessionAsync(owner, ct);
        if (tokenSession is null)
        {
            logger.LogWarning(
                "Write-back skipped for calendar owner {CalendarOwnerId}: no valid Graph access token.",
                calendarOwnerId);
            return;
        }

        await WriteBackSlotsCoreAsync(tokenSession, busySlots, placeholderTitle, calendarOwnerId, windowStart, windowEnd, ct);
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

        if (!GraphConsentAccessPolicy.AllowsInstanceWriteBack(secretData))
        {
            logger.LogInformation(
                "Write-back skipped for calendar source instance {CalendarSourceInstanceId}: Graph consent is read-only.",
                instance.Id);
            return;
        }

        var tokenSession = await CreateInstanceTokenSessionAsync(instance, ct);
        if (tokenSession is null)
        {
            logger.LogWarning(
                "Write-back skipped for calendar source instance {CalendarSourceInstanceId}: no valid Graph access token.",
                instance.Id);
            return;
        }

        await WriteBackSlotsCoreAsync(tokenSession, busySlots, placeholderTitle, instance.CalendarOwnerId, windowStart, windowEnd, ct);
    }

    private async Task WriteBackSlotsCoreAsync(
        GraphAccessTokenSession tokenSession,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        Guid calendarOwnerId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var managedEvents = await GetManagedEventsAsync(tokenSession, calendarOwnerId, windowStart, windowEnd, ct);
        var managedBySlotId = managedEvents
            .Where(e => e.GraphId is not null && e.SlotId is not null)
            .ToDictionary(e => e.SlotId!, e => e, StringComparer.Ordinal);
        var activeSlotIds = busySlots.Select(slot => slot.SourceEventId).ToHashSet(StringComparer.Ordinal);

        await UpsertPlaceholderEventsAsync(
            tokenSession,
            busySlots,
            placeholderTitle,
            calendarOwnerId,
            managedEvents,
            managedBySlotId,
            ct);
        var staleCount = await DeleteStaleManagedEventsAsync(
            tokenSession,
            managedBySlotId,
            activeSlotIds,
            calendarOwnerId,
            windowStart,
            windowEnd,
            ct);

        logger.LogInformation(
            "Write-back complete for calendar owner {CalendarOwnerId}: {UpsertCount} active placeholder(s), {DeleteCount} stale placeholder(s) removed.",
            calendarOwnerId,
            busySlots.Count,
            staleCount);
    }

    private async Task<IReadOnlyList<ManagedEventRecord>> GetManagedEventsAsync(
        GraphAccessTokenSession tokenSession,
        Guid calendarOwnerId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        using var response = await GetCalendarViewWithRetryAsync(tokenSession, windowStart, windowEnd, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Failed to fetch ObfusCal-managed Graph events for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
                calendarOwnerId,
                (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphCalendarViewResponse>(cancellationToken: ct);
        var events = await CollectAllPagesAsync(payload, tokenSession, ct);

        return events
            .Where(IsManagedEvent)
            .Select(MapManagedEvent)
            .Where(e => e.GraphId is not null)
            .ToList();
    }

    private ManagedEventRecord MapManagedEvent(GraphEvent dto)
    {
        var slotId = dto.ExtendedProperties?
            .FirstOrDefault(p => string.Equals(p.Id, SlotIdPropertyId, StringComparison.Ordinal))
            ?.Value;

        if (dto.IsAllDay)
        {
            TryParseGraphAllDayDate(dto.Start, out var allDayStart);
            TryParseGraphAllDayDate(dto.End, out var allDayEnd);
            return new ManagedEventRecord(dto.Id, slotId, dto.Subject, allDayStart, allDayEnd, true);
        }

        TryParseGraphDateTime(dto.Start, out var start);
        TryParseGraphDateTime(dto.End, out var end);
        return new ManagedEventRecord(dto.Id, slotId, dto.Subject, start, end, false);
    }

    private async Task UpsertPlaceholderEventsAsync(
        GraphAccessTokenSession tokenSession,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        Guid calendarOwnerId,
        IReadOnlyList<ManagedEventRecord> managedEvents,
        IReadOnlyDictionary<string, ManagedEventRecord> managedBySlotId,
        CancellationToken ct)
    {
        var claimedGraphIds = managedBySlotId
            .Values
            .Where(record => record.GraphId is not null)
            .Select(record => record.GraphId!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var slot in busySlots)
        {
            if (managedBySlotId.TryGetValue(slot.SourceEventId, out var existing))
            {
                claimedGraphIds.Add(existing.GraphId!);
                await UpdatePlaceholderEventIfNeededAsync(tokenSession, existing, slot, placeholderTitle, calendarOwnerId, ct);
                continue;
            }

            if (TryFindLegacyManagedEvent(managedEvents, claimedGraphIds, slot, placeholderTitle, out var legacyExisting))
            {
                claimedGraphIds.Add(legacyExisting.GraphId!);
                await UpdatePlaceholderEventIfNeededAsync(tokenSession, legacyExisting, slot, placeholderTitle, calendarOwnerId, ct);
                continue;
            }

            await CreatePlaceholderEventAsync(tokenSession, slot, placeholderTitle, calendarOwnerId, ct);
        }
    }

    private static bool TryFindLegacyManagedEvent(
        IReadOnlyList<ManagedEventRecord> managedEvents,
        IReadOnlySet<string> claimedGraphIds,
        BusySlot slot,
        string placeholderTitle,
        out ManagedEventRecord match)
    {
        var preferredTitle = BusySlotTitleComposer.Compose(slot.Title, slot.SourceName, placeholderTitle) ?? placeholderTitle;
        var candidates = managedEvents
            .Where(e => e.GraphId is not null
                && e.SlotId is null
                && !claimedGraphIds.Contains(e.GraphId!)
                && e.IsAllDay == slot.IsAllDay
                && e.Start == slot.Start
                && e.End == slot.End)
            .ToList();

        ManagedEventRecord? preferredMatch = candidates.FirstOrDefault(e =>
            string.Equals(e.Subject, preferredTitle, StringComparison.Ordinal));
        preferredMatch ??= candidates.FirstOrDefault(e =>
            string.Equals(e.Subject, placeholderTitle, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(slot.Title))
        {
            preferredMatch ??= candidates.FirstOrDefault(e =>
                string.Equals(e.Subject, slot.Title, StringComparison.Ordinal));
        }

        if (preferredMatch is not null)
        {
            match = preferredMatch;
            return true;
        }

        if (candidates.Count == 1)
        {
            match = candidates[0];
            return true;
        }

        match = default!;
        return false;
    }

    private async Task UpdatePlaceholderEventIfNeededAsync(
        GraphAccessTokenSession tokenSession,
        ManagedEventRecord existing,
        BusySlot slot,
        string placeholderTitle,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        var eventTitle = BusySlotTitleComposer.Compose(slot.Title, slot.SourceName, placeholderTitle) ?? placeholderTitle;
        var needsSlotMetadataBackfill = string.IsNullOrWhiteSpace(existing.SlotId);

        if (!needsSlotMetadataBackfill
            && existing.Start == slot.Start
            && existing.End == slot.End
            && existing.IsAllDay == slot.IsAllDay
            && string.Equals(existing.Subject, eventTitle, StringComparison.Ordinal))
        {
            return;
        }

        await PatchPlaceholderEventAsync(
            tokenSession,
            existing.GraphId!,
            slot,
            placeholderTitle,
            calendarOwnerId,
            includeSlotMetadata: needsSlotMetadataBackfill,
            ct);
    }

    private async Task<int> DeleteStaleManagedEventsAsync(
        GraphAccessTokenSession tokenSession,
        IReadOnlyDictionary<string, ManagedEventRecord> managedBySlotId,
        IReadOnlySet<string> activeSlotIds,
        Guid calendarOwnerId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var staleCount = 0;

        foreach (var (slotId, managedEvent) in managedBySlotId)
        {
            if (activeSlotIds.Contains(slotId) || managedEvent.Start < windowStart || managedEvent.Start >= windowEnd)
                continue;

            await DeleteEventAsync(tokenSession, managedEvent.GraphId!, calendarOwnerId, ct);
            staleCount++;
        }

        return staleCount;
    }

    private async Task CreatePlaceholderEventAsync(
        GraphAccessTokenSession tokenSession,
        BusySlot slot,
        string placeholderTitle,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        var eventTitle = BusySlotTitleComposer.Compose(slot.Title, slot.SourceName, placeholderTitle) ?? placeholderTitle;

        var body = CreateGraphWriteBody(slot, eventTitle, includeSlotMetadata: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphEventsPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenSession.AccessToken);
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        request.Content = new StringContent(JsonSerializer.Serialize(body, options), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return;

        logger.LogWarning(
            "Failed to create placeholder event for slot {SlotId} for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
            slot.SourceEventId,
            calendarOwnerId,
            (int)response.StatusCode);
    }

    private async Task PatchPlaceholderEventAsync(
        GraphAccessTokenSession tokenSession,
        string graphEventId,
        BusySlot slot,
        string placeholderTitle,
        Guid calendarOwnerId,
        bool includeSlotMetadata,
        CancellationToken ct)
    {
        var eventTitle = BusySlotTitleComposer.Compose(slot.Title, slot.SourceName, placeholderTitle) ?? placeholderTitle;

        var body = CreateGraphWriteBody(slot, eventTitle, includeSlotMetadata);

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{GraphEventsPath}/{Uri.EscapeDataString(graphEventId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenSession.AccessToken);
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        request.Content = new StringContent(JsonSerializer.Serialize(body, options), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return;

        logger.LogWarning(
            "Failed to patch placeholder event {GraphEventId} for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
            graphEventId,
            calendarOwnerId,
            (int)response.StatusCode);
    }

    private async Task DeleteEventAsync(
        GraphAccessTokenSession tokenSession,
        string graphEventId,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{GraphEventsPath}/{Uri.EscapeDataString(graphEventId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenSession.AccessToken);

        using var response = await httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            return;

        logger.LogWarning(
            "Failed to delete stale placeholder event {GraphEventId} for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
            graphEventId,
            calendarOwnerId,
            (int)response.StatusCode);
    }

    private object CreateGraphWriteBody(BusySlot slot, string eventTitle, bool includeSlotMetadata)
    {
        var basePayload = slot.IsAllDay
            ? new
            {
                subject = eventTitle,
                description = slot.Description,
                start = new
                {
                    dateTime = slot.Start.UtcDateTime.ToString("yyyy-MM-dd'T'00:00:00.0000000", CultureInfo.InvariantCulture),
                    timeZone = "UTC"
                },
                end = new
                {
                    dateTime = slot.End.UtcDateTime.ToString("yyyy-MM-dd'T'00:00:00.0000000", CultureInfo.InvariantCulture),
                    timeZone = "UTC"
                },
                location = string.IsNullOrWhiteSpace(slot.Location) ? null : new { displayName = slot.Location },
                isAllDay = true,
                showAs = "busy",
                isReminderOn = false
            }
            : new
            {
                subject = eventTitle,
                description = slot.Description,
                start = new { dateTime = slot.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" },
                end = new { dateTime = slot.End.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" },
                location = string.IsNullOrWhiteSpace(slot.Location) ? null : new { displayName = slot.Location },
                isAllDay = false,
                showAs = "busy",
                isReminderOn = false
            };

        if (!includeSlotMetadata)
            return basePayload;

        return new
        {
            subject = eventTitle,
            description = slot.Description,
            start = slot.IsAllDay
                ? new
                {
                    dateTime = slot.Start.UtcDateTime.ToString("yyyy-MM-dd'T'00:00:00.0000000", CultureInfo.InvariantCulture),
                    timeZone = "UTC"
                }
                : new { dateTime = slot.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" },
            end = slot.IsAllDay
                ? new
                {
                    dateTime = slot.End.UtcDateTime.ToString("yyyy-MM-dd'T'00:00:00.0000000", CultureInfo.InvariantCulture),
                    timeZone = "UTC"
                }
                : new { dateTime = slot.End.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" },
            location = string.IsNullOrWhiteSpace(slot.Location) ? null : new { displayName = slot.Location },
            isAllDay = slot.IsAllDay,
            showAs = "busy",
            isReminderOn = false,
            singleValueExtendedProperties = new[]
            {
                new { id = ManagedPropertyId, value = "1" },
                new { id = SlotIdPropertyId, value = slot.SourceEventId }
            }
        };
    }
}

