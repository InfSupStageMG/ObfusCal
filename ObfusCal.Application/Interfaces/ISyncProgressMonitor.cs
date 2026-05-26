namespace ObfusCal.Application.Interfaces;

/// <summary>
/// Read-only view of the current peer-sync run state, consumed by UI components.
/// </summary>
public interface ISyncProgressMonitor
{
    bool IsPeerSyncInProgress { get; }
    DateTimeOffset? LastPeerSyncCompletedAt { get; }
}
