using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using CoreBusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Infrastructure.Storage;

public sealed class EfCoreCalendarOwnerAvailabilitySlotStore(AppDbContext dbContext) : ICalendarOwnerAvailabilitySlotStore
{
    public async Task<IReadOnlyList<CoreBusySlot>> GetSlotsAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var entities = await dbContext.CalendarOwnerAvailabilitySlots
            .AsNoTracking()
            .Where(slot => slot.CalendarOwnerId == calendarOwnerId)
            .Where(slot => slot.Start < to && slot.End > from)
            .OrderBy(slot => slot.Start)
            .ToListAsync(ct);

        return entities.Select(slot => new CoreBusySlot(
                slot.SourceEventId,
                slot.Start,
                slot.End,
                slot.Title,
                slot.Description,
                slot.AttendeeEmails,
                slot.Location))
            .ToList();
    }
}


