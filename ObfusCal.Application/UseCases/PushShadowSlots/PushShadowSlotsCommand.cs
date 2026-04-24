using MediatR;
using ObfusCal.Domain.Models;

namespace ObfusCal.Application.UseCases.PushShadowSlots;

public record ShadowSlotInput(DateTimeOffset Start, DateTimeOffset End);

public record PushShadowSlotsCommand(
    string PeerId,
    IReadOnlyList<ShadowSlotInput> Slots) : IRequest;

