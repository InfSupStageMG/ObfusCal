using ObfusCal.Domain.Models;

namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerAvailabilitySlotStore
{
    Task<IReadOnlyList<BusySlot>> GetSlotsAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}

