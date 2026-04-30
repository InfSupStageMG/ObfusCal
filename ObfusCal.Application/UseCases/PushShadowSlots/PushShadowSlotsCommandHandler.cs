using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;

namespace ObfusCal.Application.UseCases.PushShadowSlots;

public class PushShadowSlotsUseCase(
    IShadowSlotStore shadowSlotStore,
    ILogger<PushShadowSlotsUseCase> logger)
    : IPushShadowSlotsUseCase
{
    public virtual async Task ExecuteAsync(PushShadowSlotsCommand command, CancellationToken cancellationToken)
    {
        var slots = command.Slots
            .Select((slot, index) => new BusySlot(
                $"{command.PeerId}-{index}",
                slot.Start,
                slot.End,
                slot.Title,
                slot.Description,
                slot.AttendeeEmails,
                slot.Location))
            .ToArray();

        foreach (var calendarOwnerId in command.CalendarOwnerIds.Distinct())
            await shadowSlotStore.SetSlotsAsync(command.PeerId, calendarOwnerId, slots, cancellationToken);

        logger.LogInformation(
            "Stored {BusySlotCount} pushed shadow slots for peer {PeerId} across {CalendarOwnerCount} owner scope(s)",
            slots.Length,
            command.PeerId,
            command.CalendarOwnerIds.Count);
    }
}

[Obsolete("Use PushShadowSlotsUseCase or IPushShadowSlotsUseCase instead.")]
public sealed class PushShadowSlotsCommandHandler(
    IShadowSlotStore shadowSlotStore,
    ILogger<PushShadowSlotsUseCase> logger)
    : PushShadowSlotsUseCase(shadowSlotStore, logger)
{
    public Task Handle(PushShadowSlotsCommand command, CancellationToken cancellationToken) =>
        ExecuteAsync(command, cancellationToken);
}

