using ObfusCal.Core.Models;

namespace ObfusCal.Core.Interfaces;

public interface IPeerClient
{
    Task<IReadOnlyList<BusySlot>> PullSlotsAsync(
        PeerInfo peer, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    Task PushSlotsAsync(
        PeerInfo peer, IReadOnlyList<BusySlot> slots, CancellationToken ct = default);
}