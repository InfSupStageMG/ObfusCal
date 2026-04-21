using System.Collections.Concurrent;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Infrastructure.Storage;

public sealed class InMemoryShadowSlotStore : IShadowSlotStore
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<BusySlot>> _slotsByPeer = new();

    public void SetSlots(string peerId, IReadOnlyList<BusySlot> slots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(slots);

        _slotsByPeer[peerId] = slots.ToArray();
    }

    public IReadOnlyList<BusySlot> GetSlots(string peerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);

        return _slotsByPeer.TryGetValue(peerId, out var slots)
            ? slots.ToArray()
            : [];
    }
}
