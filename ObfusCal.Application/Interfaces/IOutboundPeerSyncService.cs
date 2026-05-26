namespace ObfusCal.Application.Interfaces;

public interface IOutboundPeerSyncService
{
    Task RunSyncCycleAsync(CancellationToken ct = default, IProgress<SyncProgressUpdate>? progress = null);
}

