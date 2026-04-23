using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Infrastructure.Calendars;

public sealed class MockCalendarSource : ICalendarSource
{
    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (from > to)
        {
            throw new ArgumentException("The start of the query window must be before the end.", nameof(from));
        }

        ct.ThrowIfCancellationRequested();

        var utcNow = DateTimeOffset.UtcNow;
        var anchor = new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, TimeSpan.Zero);

        var events = CreateSeedEvents(anchor)
            .Where(calendarEvent => calendarEvent.Start >= from && calendarEvent.End <= to)
            .ToArray();

        return Task.FromResult<IReadOnlyList<CalendarEvent>>(events);
    }

    private static IReadOnlyList<CalendarEvent> CreateSeedEvents(DateTimeOffset anchor) =>
    [
        new(
            Id: "mock-team-sync",
            Title: "Team Sync",
            Description: "Weekly project check-in with delivery team.",
            Start: anchor.AddDays(1).AddHours(9),
            End: anchor.AddDays(1).AddHours(9.5),
            AttendeeEmails: ["alice@obfuscal.test", "bob@obfuscal.test"],
            Location: "Meeting Room A"),
        new(
            Id: "mock-client-workshop",
            Title: "Client Requirements Workshop",
            Description: "Review open items and confirm the next milestone scope.",
            Start: anchor.AddDays(4).AddHours(13),
            End: anchor.AddDays(4).AddHours(14.5),
            AttendeeEmails: ["calendarowner@obfuscal.test", "client@partner.test"],
            Location: "Contoso HQ"),
        new(
            Id: "mock-security-review",
            Title: "Security Review",
            Description: "Discuss privacy controls for calendar federation.",
            Start: anchor.AddDays(9).AddHours(10),
            End: anchor.AddDays(9).AddHours(11),
            AttendeeEmails: ["security@obfuscal.test"],
            Location: "Teams"),
        new(
            Id: "mock-outside-window",
            Title: "Future Planning Session",
            Description: "This event sits outside the default 14-day verification window.",
            Start: anchor.AddDays(18).AddHours(15),
            End: anchor.AddDays(18).AddHours(16),
            AttendeeEmails: ["planner@obfuscal.test"],
            Location: "Innovation Lab")
    ];
}
