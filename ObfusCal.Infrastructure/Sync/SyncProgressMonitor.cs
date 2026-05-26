using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Sync;

/// <summary>
/// Thread-safe singleton that tracks the running state of peer sync cycles.
/// Sync services use the internal write methods; UI components consume <see cref="ISyncProgressMonitor"/>.
/// </summary>
public sealed class SyncProgressMonitor : ISyncProgressMonitor
{
    // 0 = idle, 1 = running — int field lets us use Interlocked for a lock-free compare-exchange.
    private int _peerSyncRunning;


    public bool IsPeerSyncInProgress => _peerSyncRunning == 1;

    public DateTimeOffset? LastPeerSyncCompletedAt { get; private set; }


    // Returns true if this caller acquired the lock, false if a cycle is already running.
    internal bool TryBeginPeerSync()
        => Interlocked.CompareExchange(ref _peerSyncRunning, 1, 0) == 0;

    internal void EndPeerSync()
    {
        // Record completion before releasing the lock so readers never see a released lock with a stale timestamp.
        LastPeerSyncCompletedAt = DateTimeOffset.UtcNow;
        Interlocked.Exchange(ref _peerSyncRunning, 0);
    }
}
