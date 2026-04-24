using MediatR;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;

namespace ObfusCal.Application.UseCases.PushShadowSlots;

internal sealed class PushShadowSlotsCommandHandler(
    IShadowSlotStore shadowSlotStore,
    ILogger<PushShadowSlotsCommandHandler> logger)
    : IRequestHandler<PushShadowSlotsCommand>
{
    public async Task Handle(PushShadowSlotsCommand command, CancellationToken ct)
    {
        var slots = command.Slots
            .Select((slot, index) => new BusySlot($"{command.PeerId}-{index}", slot.Start, slot.End))
            .ToArray();

        await shadowSlotStore.SetSlotsAsync(command.PeerId, slots, ct);

        logger.LogInformation(
            "Stored {BusySlotCount} pushed shadow slots for peer {PeerId}",
            slots.Length,
            command.PeerId);
    }
}

