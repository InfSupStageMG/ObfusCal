using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;

namespace ObfusCal.Infrastructure.Sync;

public sealed class CalendarOwnerClientBusySlotService(
    ICalendarOwnerAvailabilitySlotStore availabilitySlotStore,
    ObfuscationPipeline obfuscationPipeline,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService) : ICalendarOwnerClientBusySlotService
{
    public async Task<IReadOnlyList<BusySlot>> BuildAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        // Read already-obfuscated slots from the database (internal obfuscation has been applied during sync)
        var savedSlots = await availabilitySlotStore.GetSlotsAsync(calendarOwnerId, from, to, ct);

        // Convert back to CalendarEvents so we can run them through the client obfuscation pipeline
        var eventsFromSlots = savedSlots.Select(slot => new CalendarEvent(
            slot.SourceEventId,
            slot.Title ?? string.Empty,
            slot.Description,
            slot.Start,
            slot.End,
            slot.AttendeeEmails ?? [],
            slot.Location,
            IsAllDay: slot.IsAllDay
        )).ToList();

        // Apply client-level obfuscation
        var clientProfile = await obfuscationProfileService.GetProfileAsync(
            calendarOwnerId,
            ObfuscationAuditContext.Client,
            ct);

        return obfuscationPipeline.Process(
            eventsFromSlots,
            calendarOwnerId.ToString(),
            ObfuscationAuditContext.Client,
            clientProfile);
    }
}

