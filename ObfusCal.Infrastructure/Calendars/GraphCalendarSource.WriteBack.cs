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
        var owner = await _dbContext.CalendarOwners
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId, ct);
        if (owner is null)
            return;

        if (!AllowsWriteBack(owner.GraphGrantedScopes))
        {
            _logger.LogInformation(
                "Write-back skipped for calendar owner {CalendarOwnerId}: Graph consent is read-only.",
                calendarOwnerId);
            return;
        }

        var tokenSession = await CreateOwnerTokenSessionAsync(owner, ct);
        if (tokenSession is null)
        {
            _logger.LogWarning(
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

        var instanceScopes = secretData?.GrantedScopes;
        if (string.IsNullOrWhiteSpace(instanceScopes)
            || !instanceScopes.Contains("Calendars.ReadWrite", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Write-back skipped for calendar source instance {CalendarSourceInstanceId}: Graph consent is read-only.",
                instance.Id);
            return;
        }

        var tokenSession = await CreateInstanceTokenSessionAsync(instance, ct);
        if (tokenSession is null)
        {
            _logger.LogWarning(
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
        var managedBySlotId = await GetManagedEventsBySlotIdAsync(tokenSession, calendarOwnerId, windowStart, windowEnd, ct);
        var activeSlotIds = busySlots.Select(slot => slot.SourceEventId).ToHashSet(StringComparer.Ordinal);

        await UpsertPlaceholderEventsAsync(tokenSession, busySlots, placeholderTitle, calendarOwnerId, managedBySlotId, ct);
        var staleCount = await DeleteStaleManagedEventsAsync(
            tokenSession,
            managedBySlotId,
            activeSlotIds,
            calendarOwnerId,
            windowStart,
            windowEnd,
            ct);

        _logger.LogInformation(
            "Write-back complete for calendar owner {CalendarOwnerId}: {UpsertCount} active placeholder(s), {DeleteCount} stale placeholder(s) removed.",
            calendarOwnerId,
            busySlots.Count,
            staleCount);
    }

    private async Task<Dictionary<string, ManagedEventRecord>> GetManagedEventsBySlotIdAsync(
        GraphAccessTokenSession tokenSession,
        Guid calendarOwnerId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var managedEvents = await GetManagedEventsAsync(tokenSession, calendarOwnerId, windowStart, windowEnd, ct);

        return managedEvents
            .Where(e => e.GraphId is not null && e.SlotId is not null)
            .ToDictionary(e => e.SlotId!, e => e, StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<ManagedEventRecord>> GetManagedEventsAsync(
        GraphAccessTokenSession tokenSession,
        Guid calendarOwnerId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var requestUri = BuildManagedEventsRequestUri(windowStart, windowEnd);
        var events = new List<ManagedEventRecord>();
        string? nextPageUri = requestUri;

        while (!string.IsNullOrWhiteSpace(nextPageUri))
        {
            using var response = await SendAuthorizedGetWithRetryAsync(nextPageUri, tokenSession, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch ObfusCal-managed Graph events for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
                    calendarOwnerId,
                    (int)response.StatusCode);
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<GraphManagedEventsResponse>(cancellationToken: ct);
            if (payload?.Value is not null)
                events.AddRange(payload.Value.Select(MapManagedEvent).Where(e => e.GraphId is not null));

            nextPageUri = payload?.NextLink;
        }

        return events;
    }

    private string BuildManagedEventsRequestUri(DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        var managedFilter = $"singleValueExtendedProperties/Any(ep: ep/id eq '{ManagedPropertyId}' and ep/value eq '1')";
        var windowFilter =
            $"start/dateTime ge '{windowStart.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}' and start/dateTime lt '{windowEnd.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}'";
        var filter = $"{managedFilter} and {windowFilter}";
        var expand = $"singleValueExtendedProperties($filter=id eq '{SlotIdPropertyId}')";

        return $"{GraphEventsPath}?$filter={Uri.EscapeDataString(filter)}&$expand={Uri.EscapeDataString(expand)}&$select=id,subject,start,end&$top={GraphCalendarViewPageSize}";
    }

    private ManagedEventRecord MapManagedEvent(GraphManagedEventDto dto)
    {
        var slotId = dto.ExtendedProperties?
            .FirstOrDefault(p => string.Equals(p.Id, SlotIdPropertyId, StringComparison.Ordinal))
            ?.Value;

        TryParseGraphDateTime(dto.Start, out var start);
        TryParseGraphDateTime(dto.End, out var end);
        return new ManagedEventRecord(dto.Id, slotId, dto.Subject, start, end);
    }

    private async Task UpsertPlaceholderEventsAsync(
        GraphAccessTokenSession tokenSession,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        Guid calendarOwnerId,
        IReadOnlyDictionary<string, ManagedEventRecord> managedBySlotId,
        CancellationToken ct)
    {
        foreach (var slot in busySlots)
        {
            if (managedBySlotId.TryGetValue(slot.SourceEventId, out var existing))
            {
                await UpdatePlaceholderEventIfNeededAsync(tokenSession, existing, slot, placeholderTitle, calendarOwnerId, ct);
                continue;
            }

            await CreatePlaceholderEventAsync(tokenSession, slot, placeholderTitle, calendarOwnerId, ct);
        }
    }

    private async Task UpdatePlaceholderEventIfNeededAsync(
        GraphAccessTokenSession tokenSession,
        ManagedEventRecord existing,
        BusySlot slot,
        string placeholderTitle,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        if (existing.Start == slot.Start
            && existing.End == slot.End
            && string.Equals(existing.Subject, placeholderTitle, StringComparison.Ordinal))
        {
            return;
        }

        await PatchPlaceholderEventAsync(tokenSession, existing.GraphId!, slot, placeholderTitle, calendarOwnerId, ct);
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
        var body = new
        {
            subject = placeholderTitle,
            start = new { dateTime = slot.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" },
            end = new { dateTime = slot.End.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" },
            showAs = "busy",
            isReminderOn = false,
            singleValueExtendedProperties = new[]
            {
                new { id = ManagedPropertyId, value = "1" },
                new { id = SlotIdPropertyId, value = slot.SourceEventId }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphEventsPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenSession.AccessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return;

        _logger.LogWarning(
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
        CancellationToken ct)
    {
        var body = new
        {
            subject = placeholderTitle,
            start = new { dateTime = slot.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" },
            end = new { dateTime = slot.End.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{GraphEventsPath}/{Uri.EscapeDataString(graphEventId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenSession.AccessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return;

        _logger.LogWarning(
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

        using var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            return;

        _logger.LogWarning(
            "Failed to delete stale placeholder event {GraphEventId} for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
            graphEventId,
            calendarOwnerId,
            (int)response.StatusCode);
    }
}

