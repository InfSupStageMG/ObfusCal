using ObfusCal.Domain.Models;

namespace ObfusCal.Application.Interfaces;

public interface IShadowSlotStore
{
    Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default);
    Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default);
    Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

