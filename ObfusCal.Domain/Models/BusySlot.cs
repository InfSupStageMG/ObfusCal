namespace ObfusCal.Domain.Models;

public record BusySlot(
    string SourceEventId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Title = null,
    string? Description = null,
    IReadOnlyList<string>? AttendeeEmails = null,
    string? Location = null,
    string? SourceLabel = null,
    IReadOnlyList<BusySlot>? SourceSlots = null,
    bool IsAllDay = false,
    string? ColorHex = null,
    string? SourceName = null
);
