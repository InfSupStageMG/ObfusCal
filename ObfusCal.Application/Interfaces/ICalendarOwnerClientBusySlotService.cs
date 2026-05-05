using ObfusCal.Domain.Models;

namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerClientBusySlotService
{
    Task<IReadOnlyList<BusySlot>> BuildAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}

