using MediatR;
using ObfusCal.Domain.Models;

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
    IReadOnlyList<ShadowSlotInput> Slots) : IRequest;

