namespace ObfusCal.Application.UseCases.PushShadowSlots;

public record ShadowSlotInput(
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Title = null,
    string? Description = null,
    IReadOnlyList<string>? AttendeeEmails = null,
    string? Location = null);

public record PushShadowSlotsCommand(
    string PeerId,
    IReadOnlyList<Guid> CalendarOwnerIds,
    IReadOnlyList<ShadowSlotInput> Slots);

