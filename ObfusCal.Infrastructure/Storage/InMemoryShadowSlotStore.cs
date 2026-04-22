using System.Collections.Concurrent;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Infrastructure.Storage;

public sealed class InMemoryShadowSlotStore : IShadowSlotStore
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<BusySlot>> _slotsByPeer = new();

    public Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(slots);
        ct.ThrowIfCancellationRequested();
        _slotsByPeer[peerId] = slots.ToArray();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ct.ThrowIfCancellationRequested();
        var result = _slotsByPeer.TryGetValue(peerId, out var slots)
            ? slots.ToArray()
            : [];
        return Task.FromResult<IReadOnlyList<BusySlot>>(result);
    }
}
