using ObfusCal.Domain.Models;

namespace ObfusCal.Application.UseCases.GetMergedFreeBusy;

public record MergedFreeBusyResponse(
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Title = null,
    string? Description = null,
    IReadOnlyList<string>? AttendeeEmails = null,
    string? Location = null,
    string? SourceLabel = null,
    IReadOnlyList<BusySlot>? SourceSlots = null,
    bool IsAllDay = false,
    string? ColorHex = null
);
