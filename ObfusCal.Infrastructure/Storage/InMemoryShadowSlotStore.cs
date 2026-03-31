using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Infrastructure.Storage;

// Week 1: in-memory only. Week 2: swap to SqliteShadowSlotStore : IShadowSlotStore
public class InMemoryShadowSlotStore : IShadowSlotStore
{
    private readonly Dictionary<string, List<BusySlot>> _store = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task SaveAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try { _store[peerId] = [..slots]; }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<BusySlot>> GetAsync(
        string peerId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_store.TryGetValue(peerId, out var slots)) return [];
            return slots.Where(s => s.Start >= from && s.End <= to).ToList();
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<string>> GetPeerIdsAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try { return [.._store.Keys]; }
        finally { _lock.Release(); }
    }
}