namespace ObfusCal.Core.Models;

public record CalendarEvent(
    string Id,
    string Title,
    string? Description,
    DateTimeOffset Start,
    DateTimeOffset End,
    IReadOnlyList<string> AttendeeEmails,
    string? Location
);