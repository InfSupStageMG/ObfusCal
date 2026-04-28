namespace ObfusCal.Application.Interfaces;

public interface IOutboundPeerSyncService
{
    Task RunSyncCycleAsync(CancellationToken ct = default);
}

