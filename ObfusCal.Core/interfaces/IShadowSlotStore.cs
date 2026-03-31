using ObfusCal.Core.Models;

namespace ObfusCal.Core.Interfaces;

public interface IShadowSlotStore
{
    Task SaveAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default);

    Task<IReadOnlyList<BusySlot>> GetAsync(
        string peerId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetPeerIdsAsync(CancellationToken ct = default);
}