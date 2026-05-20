using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;

namespace ObfusCal.Infrastructure.Calendars;

public sealed partial class ICloudCalendarSourceCore
{
    private const string ManagedXProperty = "X-OBFUSCAL-MANAGED";
    private const string SlotIdXProperty = "X-OBFUSCAL-SLOT-ID";
    private const string ManagedXPropertyValue = "TRUE";
    private const string ManagedEventUidPrefix = "obfuscal-";

    public async Task WriteBackSlotsAsync(
        Guid calendarOwnerId,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var owner = await dbContext.CalendarOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId, ct);

        if (owner is null)
            return;

        var configuration = await TryBuildConfigurationAsync(owner, ct);
        if (configuration is null)
        {
            logger.LogWarning(
                "Write-back skipped for calendar owner {CalendarOwnerId}: iCloud configuration is not available.",
                calendarOwnerId);
            return;
        }

        await WriteBackSlotsCoreAsync(calendarOwnerId, configuration, busySlots, placeholderTitle, windowStart,
            windowEnd, ct);
    }

    public async Task WriteBackSlotsAsync(
        CalendarSourceInstanceContext instance,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var configuration = await TryBuildConfigurationAsync(instance, ct);
        if (configuration is null)
        {
            logger.LogWarning(
                "Write-back skipped for calendar source instance {CalendarSourceInstanceId}: iCloud configuration is not available.",
                instance.Id);
            return;
        }

        await WriteBackSlotsCoreAsync(instance.CalendarOwnerId, configuration, busySlots, placeholderTitle,
            windowStart, windowEnd, ct);
    }

    private async Task WriteBackSlotsCoreAsync(
        Guid calendarOwnerId,
        ICloudCalendarOwnerConfiguration configuration,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var managedBySlotId =
            await GetManagedEventsBySlotIdAsync(calendarOwnerId, configuration, windowStart, windowEnd, ct);
        var activeSlotIds = busySlots.Select(s => s.SourceEventId).ToHashSet(StringComparer.Ordinal);

        await UpsertPlaceholderEventsAsync(calendarOwnerId, configuration, busySlots, placeholderTitle,
            managedBySlotId, ct);

        var staleCount = await DeleteStaleManagedEventsAsync(calendarOwnerId, configuration, managedBySlotId,
            activeSlotIds, windowStart, windowEnd, ct);

        logger.LogInformation(
            "Write-back complete for iCloud calendar owner {CalendarOwnerId}: {UpsertCount} active placeholder(s), {DeleteCount} stale placeholder(s) removed.",
            calendarOwnerId,
            busySlots.Count,
            staleCount);
    }

    private async Task<Dictionary<string, ManagedCalDavEvent>> GetManagedEventsBySlotIdAsync(
        Guid calendarOwnerId,
        ICloudCalendarOwnerConfiguration configuration,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var managedEvents =
            await FetchManagedCalDavEventsAsync(calendarOwnerId, configuration, windowStart, windowEnd, ct);
        return managedEvents
            .Where(e => e.SlotId is not null)
            .GroupBy(e => e.SlotId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<ManagedCalDavEvent>> FetchManagedCalDavEventsAsync(
        Guid calendarOwnerId,
        ICloudCalendarOwnerConfiguration configuration,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        try
        {
            using var request = CreateCalendarQueryRequest(configuration, windowStart, windowEnd);
            using var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to fetch CalDAV events for write-back for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
                    calendarOwnerId,
                    (int)response.StatusCode);
                return [];
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            return ParseManagedCalDavEvents(responseBody);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to fetch CalDAV managed events for calendar owner {CalendarOwnerId}.",
                calendarOwnerId);
            return [];
        }
    }

    private async Task UpsertPlaceholderEventsAsync(
        Guid calendarOwnerId,
        ICloudCalendarOwnerConfiguration configuration,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        IReadOnlyDictionary<string, ManagedCalDavEvent> managedBySlotId,
        CancellationToken ct)
    {
        foreach (var slot in busySlots)
        {
            // Skip if already up-to-date
            if (managedBySlotId.TryGetValue(slot.SourceEventId, out var existing)
                && existing.Start == slot.Start
                && existing.End == slot.End
                && string.Equals(existing.Summary, placeholderTitle, StringComparison.Ordinal))
            {
                continue;
            }

            await PutPlaceholderEventAsync(calendarOwnerId, configuration, slot, placeholderTitle, ct);
        }
    }

    private async Task PutPlaceholderEventAsync(
        Guid calendarOwnerId,
        ICloudCalendarOwnerConfiguration configuration,
        BusySlot slot,
        string placeholderTitle,
        CancellationToken ct)
    {
        var uid = GetManagedEventUid(slot.SourceEventId);
        var resourceName = uid + ".ics";
        var calendarBase = configuration.CalendarUri.ToString().TrimEnd('/') + "/";
        var eventUri = new Uri(calendarBase + resourceName);
        var icsContent = BuildPlaceholderIcsContent(uid, slot, placeholderTitle);

        using var request = new HttpRequestMessage(HttpMethod.Put, eventUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{configuration.AppleId}:{configuration.AppSpecificPassword}")));
        request.Content = new StringContent(icsContent, Encoding.UTF8, "text/calendar");

        try
        {
            using var response = await httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return;

            logger.LogWarning(
                "Failed to PUT placeholder event for slot {SlotId} for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
                slot.SourceEventId,
                calendarOwnerId,
                (int)response.StatusCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Exception while writing placeholder event for slot {SlotId} for calendar owner {CalendarOwnerId}.",
                slot.SourceEventId,
                calendarOwnerId);
        }
    }

    private async Task<int> DeleteStaleManagedEventsAsync(
        Guid calendarOwnerId,
        ICloudCalendarOwnerConfiguration configuration,
        IReadOnlyDictionary<string, ManagedCalDavEvent> managedBySlotId,
        IReadOnlySet<string> activeSlotIds,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct)
    {
        var staleCount = 0;

        foreach (var (slotId, managedEvent) in managedBySlotId)
        {
            if (activeSlotIds.Contains(slotId)
                || managedEvent.Start < windowStart
                || managedEvent.Start >= windowEnd)
            {
                continue;
            }

            await DeleteManagedEventAsync(calendarOwnerId, configuration, managedEvent.Href, ct);
            staleCount++;
        }

        return staleCount;
    }

    private async Task DeleteManagedEventAsync(
        Guid calendarOwnerId,
        ICloudCalendarOwnerConfiguration configuration,
        string href,
        CancellationToken ct)
    {
        // On Linux, Uri.TryCreate("/absolute/path", UriKind.Absolute, ...) succeeds and produces
        // a file:/// URI, which is wrong for CalDAV hrefs. Avoid that by only accepting hrefs that
        // already carry an explicit http(s) scheme; everything else is resolved against the server origin.
        var deleteUri = ResolveCalDavHref(href, configuration.CalendarUri);

        using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{configuration.AppleId}:{configuration.AppSpecificPassword}")));

        try
        {
            using var response = await httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                return;

            logger.LogWarning(
                "Failed to DELETE managed CalDAV event at {Href} for calendar owner {CalendarOwnerId}: HTTP {StatusCode}.",
                href,
                calendarOwnerId,
                (int)response.StatusCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Exception while deleting managed CalDAV event at {Href} for calendar owner {CalendarOwnerId}.",
                href,
                calendarOwnerId);
        }
    }

    internal static IReadOnlyList<ManagedCalDavEvent> ParseManagedCalDavEvents(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return [];

        try
        {
            var document = XDocument.Parse(responseBody);
            var managedEvents = new List<ManagedCalDavEvent>();

            foreach (var responseElement in document.Descendants()
                         .Where(e => string.Equals(e.Name.LocalName, "response",
                             StringComparison.OrdinalIgnoreCase)))
            {
                var href = responseElement.Descendants()
                    .FirstOrDefault(e =>
                        string.Equals(e.Name.LocalName, "href", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (string.IsNullOrWhiteSpace(href))
                    continue;

                var calendarData = responseElement.Descendants()
                    .FirstOrDefault(e =>
                        string.Equals(e.Name.LocalName, "calendar-data", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (string.IsNullOrWhiteSpace(calendarData))
                    continue;

                var managedEvent = TryParseAsManagedCalDavEvent(href, calendarData);
                if (managedEvent is not null)
                    managedEvents.Add(managedEvent);
            }

            return managedEvents;
        }
        catch
        {
            return [];
        }
    }

    private static ManagedCalDavEvent? TryParseAsManagedCalDavEvent(string href, string calendarData)
    {
        var lines = UnfoldCalDavLines(calendarData);
        var isManaged = false;
        string? slotId = null;
        var start = default(DateTimeOffset);
        var end = default(DateTimeOffset);
        string? summary = null;

        foreach (var line in lines)
        {
            if (line.StartsWith(ManagedXProperty + ":", StringComparison.OrdinalIgnoreCase))
            {
                isManaged = string.Equals(
                    line[(ManagedXProperty.Length + 1)..].Trim(),
                    ManagedXPropertyValue,
                    StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (line.StartsWith(SlotIdXProperty + ":", StringComparison.OrdinalIgnoreCase))
            {
                slotId = line[(SlotIdXProperty.Length + 1)..].Trim();
                continue;
            }

            if (line.StartsWith("DTSTART", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                    TryParseCalDavUtcDateTime(line[(colonIdx + 1)..].Trim(), out start);
                continue;
            }

            if (line.StartsWith("DTEND", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                    TryParseCalDavUtcDateTime(line[(colonIdx + 1)..].Trim(), out end);
                continue;
            }

            if (line.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
            {
                summary = line["SUMMARY:".Length..].Trim();
            }
        }

        return isManaged ? new ManagedCalDavEvent(href, slotId, summary, start, end) : null;
    }

    private static bool TryParseCalDavUtcDateTime(string value, out DateTimeOffset result)
    {
        if (DateTimeOffset.TryParseExact(
                value,
                "yyyyMMdd'T'HHmmss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    private static List<string> UnfoldCalDavLines(string content)
    {
        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var unfolded = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrEmpty(line))
                continue;

            if ((line[0] == ' ' || line[0] == '\t') && unfolded.Count > 0)
                unfolded[^1] += line.TrimStart();
            else
                unfolded.Add(line);
        }

        return unfolded;
    }

    internal static string GetManagedEventUid(string slotId)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(slotId));
        return ManagedEventUidPrefix + Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    internal static string BuildPlaceholderIcsContent(string uid, BusySlot slot, string placeholderTitle)
    {
        var startStamp = slot.Start.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var endStamp = slot.End.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dtstamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var escapedTitle = EscapeIcsText(placeholderTitle);

        return string.Join("\r\n", [
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//ObfusCal//ObfusCal//EN",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{uid}",
            $"SUMMARY:{escapedTitle}",
            $"DTSTART:{startStamp}",
            $"DTEND:{endStamp}",
            $"DTSTAMP:{dtstamp}",
            "TRANSP:OPAQUE",
            $"{ManagedXProperty}:{ManagedXPropertyValue}",
            $"{SlotIdXProperty}:{slot.SourceEventId}",
            "END:VEVENT",
            "END:VCALENDAR",
            string.Empty
        ]);
    }

    private static string EscapeIcsText(string text)
        => text
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\n", "\\n");

    internal sealed record ManagedCalDavEvent(
        string Href,
        string? SlotId,
        string? Summary,
        DateTimeOffset Start,
        DateTimeOffset End);
}


