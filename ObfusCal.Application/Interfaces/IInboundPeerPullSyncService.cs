namespace ObfusCal.Application.Interfaces;

public interface IInboundPeerPullSyncService
{
    Task RunSyncCycleAsync(CancellationToken ct = default);
}

