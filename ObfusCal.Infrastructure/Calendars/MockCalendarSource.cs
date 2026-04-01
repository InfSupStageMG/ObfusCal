using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Infrastructure.Calendars;

public class MockCalendarSource: ICalendarSource
{
    private static readonly List<CalendarEvent> Events =
    [
        new("evt-1", "Sprint planning", "Q2 sprint kickoff",
            DateTimeOffset.UtcNow.Date.AddHours(9),
            DateTimeOffset.UtcNow.Date.AddHours(10),
            ["alice@infosupport.com", "bob@infosupport.com"], "Room 4A"),

        new("evt-2", "Client review: Project X", "Confidential — NDA applies",
            DateTimeOffset.UtcNow.Date.AddHours(13),
            DateTimeOffset.UtcNow.Date.AddHours(14),
            ["manager@client.com"], "Teams call"),

        new("evt-3", "1-on-1 with manager", null,
            DateTimeOffset.UtcNow.Date.AddDays(1).AddHours(11),
            DateTimeOffset.UtcNow.Date.AddDays(1).AddHours(11.5),
            ["manager@infosupport.com"], null),
    ];

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var result = Events
            .Where(e => e.Start >= from && e.End <= to)
            .ToList();

        return Task.FromResult<IReadOnlyList<CalendarEvent>>(result);
    }
}