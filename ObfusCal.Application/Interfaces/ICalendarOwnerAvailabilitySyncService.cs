namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerAvailabilitySyncService
{
    Task RunSyncCycleAsync(CancellationToken ct = default);
    Task RunSyncForOwnerAsync(Guid calendarOwnerId, CancellationToken ct = default);
}
