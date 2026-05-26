namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerAvailabilitySyncService
{
    Task RunSyncCycleAsync(CancellationToken ct = default, IProgress<SyncProgressUpdate>? progress = null);
    Task RunSyncForOwnerAsync(Guid calendarOwnerId, CancellationToken ct = default, IProgress<SyncProgressUpdate>? progress = null);
}
