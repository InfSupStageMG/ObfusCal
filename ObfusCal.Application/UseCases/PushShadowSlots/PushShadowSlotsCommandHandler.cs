using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.UseCases.Validation;
using ObfusCal.Domain.Models;

namespace ObfusCal.Application.UseCases.PushShadowSlots;

public sealed class PushShadowSlotsUseCase(
    IShadowSlotStore shadowSlotStore,
    IOptions<SyncOptions> syncOptions,
    ILogger<PushShadowSlotsUseCase> logger)
    : IPushShadowSlotsUseCase
{
    public async Task ExecuteAsync(PushShadowSlotsCommand command, CancellationToken cancellationToken)
    {
        var maxSlots = Math.Max(1, syncOptions.Value.MaxShadowSlotsPerRequest);
        if (command.Slots.Count > maxSlots)
        {
            throw new RequestValidationException(
                "slots",
                $"A maximum of {maxSlots} slot(s) is allowed per request.");
        }

        if (command.Slots.Any(slot => slot.End <= slot.Start))
            throw new RequestValidationException("slots", "Each slot must have an end time greater than its start time.");

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


