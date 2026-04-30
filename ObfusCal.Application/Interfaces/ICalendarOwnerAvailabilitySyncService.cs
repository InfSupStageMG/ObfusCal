namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerAvailabilitySyncService
{
    Task RunSyncCycleAsync(CancellationToken ct = default);
}

