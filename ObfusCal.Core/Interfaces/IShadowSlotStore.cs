using ObfusCal.Core.Models;

namespace ObfusCal.Core.Interfaces;

public interface IShadowSlotStore
{
    Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default);
    Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default);
}
