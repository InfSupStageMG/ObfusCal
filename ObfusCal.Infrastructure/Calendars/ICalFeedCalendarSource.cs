using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Calendars;

/// <summary>
/// Calendar source that imports events from one or more owner-configured iCal feed URLs.
/// Each feed is fetched independently; failure on one feed does not prevent others from loading.
/// Falls back to <see cref="MockCalendarSource"/> when the owner has no feeds configured.
/// </summary>

[CalendarSourcePlugin("ical", "iCal feed")]
public sealed class IcalFeedCalendarSource(
    HttpClient httpClient,
    AppDbContext dbContext,
    MockCalendarSource fallbackSource,
    ILogger<IcalFeedCalendarSource> logger) : ICalendarSource, ICalendarSourceReadinessEvaluator
{
    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateTimeOffset from,
        DateTimeOffset to,
        Guid? calendarOwnerId = null,
        CancellationToken ct = default)
    {
        if (from > to)
            throw new ArgumentException("The start of the query window must be before the end.", nameof(from));

        ct.ThrowIfCancellationRequested();

        if (calendarOwnerId is null)
            return await fallbackSource.GetEventsAsync(from, to, ct: ct);

        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(x => x.Id == calendarOwnerId.Value, ct);

        if (!ownerExists)
            return [];

        var feeds = await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .Where(f => f.CalendarOwnerId == calendarOwnerId.Value)
            .ToListAsync(ct);

        if (feeds.Count == 0)
            return await fallbackSource.GetEventsAsync(from, to, calendarOwnerId, ct);

        var allEvents = new List<CalendarEvent>();

        foreach (var feed in feeds)
        {
            var feedEvents = await FetchFeedEventsAsync(feed.FeedUrl, calendarOwnerId.Value, ct);
            allEvents.AddRange(feedEvents);
        }

        return allEvents
            .Where(e => e.Start < to && e.End > from)
            .OrderBy(e => e.Start)
            .ToList();
    }

    public async Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(x => x.Id == calendarOwnerId, ct);

        if (!ownerExists)
            return CalendarSourceReadiness.NotReady("Calendar owner not found.");

        var feedCount = await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .CountAsync(feed => feed.CalendarOwnerId == calendarOwnerId, ct);

        return feedCount > 0
            ? CalendarSourceReadiness.Ready("At least one iCal feed is configured.")
            : CalendarSourceReadiness.NotReady(
                "iCal feed required.",
                "Add at least one iCal feed before requesting busy slots from the iCal provider.");
    }

    private async Task<IReadOnlyList<CalendarEvent>> FetchFeedEventsAsync(
        string feedUrl,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var feedUri))
        {
            logger.LogWarning(
                "Calendar owner {CalendarOwnerId} has an invalid iCal feed URL: {FeedUrl}.",
                calendarOwnerId,
                feedUrl);
            return [];
        }

        try
        {
            using var response = await httpClient.GetAsync(feedUri, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "iCal feed fetch failed for calendar owner {CalendarOwnerId} at {FeedUrl} with HTTP {StatusCode}.",
                    calendarOwnerId,
                    feedUrl,
                    (int)response.StatusCode);
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            return ParseIcsEvents(content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "iCal feed fetch or parse failed for calendar owner {CalendarOwnerId} at {FeedUrl}.",
                calendarOwnerId,
                feedUrl);
            return [];
        }
    }

    private static List<CalendarEvent> ParseIcsEvents(string icsContent)
    {
        if (string.IsNullOrWhiteSpace(icsContent))
            return [];

        var events = new List<CalendarEvent>();
        var unfoldedLines = UnfoldLines(icsContent);

        Dictionary<string, List<string>>? current = null;
        foreach (var line in unfoldedLines)
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null && TryMapEvent(current, out var calendarEvent))
                    events.Add(calendarEvent);

                current = null;
                continue;
            }

            ProcessEventLine(line, current);
        }

        return events;
    }

    private static void ProcessEventLine(string line, Dictionary<string, List<string>>? current)
    {
        var colonIndex = line.IndexOf(':');
        if (current is null || colonIndex <= 0)
            return;

        var rawKey = line[..colonIndex];
        var value = line[(colonIndex + 1)..].Trim();
        var key = rawKey.Split(';', 2)[0];

        if (!current.TryGetValue(key, out var values))
        {
            values = [];
            current[key] = values;
        }

        values.Add(value);

        ExtractTimeZoneId(rawKey, key, current);
    }

    private static void ExtractTimeZoneId(string rawKey, string key, Dictionary<string, List<string>> current)
    {
        if (!rawKey.Contains(';') ||
            (!key.Equals("DTSTART", StringComparison.OrdinalIgnoreCase) &&
             !key.Equals("DTEND", StringComparison.OrdinalIgnoreCase)))
            return;

        var tzIdParam = rawKey
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .FirstOrDefault(p => p.StartsWith("TZID=", StringComparison.OrdinalIgnoreCase));

        if (tzIdParam is null)
            return;

        var tzKey = $"{key}#TZID";
        if (!current.TryGetValue(tzKey, out var tzValues))
        {
            tzValues = [];
            current[tzKey] = tzValues;
        }
        tzValues.Add(tzIdParam["TZID=".Length..]);
    }


    private static List<string> UnfoldLines(string content)
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
            {
                unfolded[^1] += line.TrimStart();
            }
            else
            {
                unfolded.Add(line);
            }
        }

        return unfolded;
    }

    private static bool TryMapEvent(
        IReadOnlyDictionary<string, List<string>> values,
        out CalendarEvent calendarEvent)
    {
        calendarEvent = null!;

        if (!values.TryGetValue("DTSTART", out var startValues)
            || !TryParseIcsDateTime(startValues[0], TryGetFirst(values, "DTSTART#TZID"), out var start))
        {
            return false;
        }

        DateTimeOffset end;
        if (values.TryGetValue("DTEND", out var endValues)
            && TryParseIcsDateTime(endValues[0], TryGetFirst(values, "DTEND#TZID"), out var parsedEnd))
        {
            end = parsedEnd;
        }
        else
        {
            end = startValues[0].Contains('T', StringComparison.OrdinalIgnoreCase)
                ? start.AddMinutes(30)
                : start.AddDays(1);
        }

        if (end <= start)
        {
            if (end == start
                && IsDateOnlyValue(startValues[0])
                && values.TryGetValue("DTEND", out var rawEndValues)
                && rawEndValues.Count > 0
                && IsDateOnlyValue(rawEndValues[0]))
            {
                end = start.AddDays(1);
            }
            else
            {
                return false;
            }
        }

        var id = TryGetFirst(values, "UID") ?? Guid.NewGuid().ToString("N");
        var title = TryGetFirst(values, "SUMMARY") ?? "Busy";
        var description = TryGetFirst(values, "DESCRIPTION");
        var location = TryGetFirst(values, "LOCATION");

        var attendees = values.TryGetValue("ATTENDEE", out var attendeeValues)
            ? attendeeValues.Select(ParseAttendee).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
            : [];

        calendarEvent = new CalendarEvent(
            id,
            title,
            description,
            start,
            end,
            attendees,
            location);

        return true;
    }

    private static string? TryGetFirst(IReadOnlyDictionary<string, List<string>> values, string key)
        => values.TryGetValue(key, out var list) && list.Count > 0 ? list[0] : null;

    private static bool IsDateOnlyValue(string value)
        => !value.Contains('T', StringComparison.OrdinalIgnoreCase);

    private static string ParseAttendee(string raw)
    {
        return raw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? raw[7..] : raw;
    }

    private static bool TryParseIcsDateTime(string value, string? tzId, out DateTimeOffset result)
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

        if (DateTime.TryParseExact(
                value,
                "yyyyMMdd'T'HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var localDt))
        {
            if (!string.IsNullOrWhiteSpace(tzId))
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                    var utcDt = TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(localDt, DateTimeKind.Unspecified), tz);
                    result = new DateTimeOffset(utcDt, TimeSpan.Zero);
                    return true;
                }
                catch (TimeZoneNotFoundException)
                {
                    // Unrecognised TZID on this host; fall through and treat as floating (UTC).
                }
            }

            result = new DateTimeOffset(localDt, TimeSpan.Zero);
            return true;
        }

        if (DateTime.TryParseExact(
                value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnly))
        {
            result = new DateTimeOffset(DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc));
            return true;
        }

        result = default;
        return false;
    }
}
