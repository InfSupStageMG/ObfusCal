using System.Collections.Concurrent;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using Serilog;

namespace ObfusCal.Infrastructure.Storage;

public sealed class InMemoryShadowSlotStore(ILogger logger) : IShadowSlotStore
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<BusySlot>> _slotsByPeer = new();
    private readonly ILogger _logger = logger.ForContext<InMemoryShadowSlotStore>();

    public Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(slots);
        ct.ThrowIfCancellationRequested();

        _slotsByPeer[peerId] = slots.ToArray();

        _logger.ForContext("PeerId", peerId)
            .ForContext("BusySlotCount", slots.Count)
            .Information("Stored shadow slots for peer");

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ct.ThrowIfCancellationRequested();

        var result = _slotsByPeer.TryGetValue(peerId, out var slots) ? slots.ToArray() :[];

        _logger.ForContext("PeerId", peerId)
            .ForContext("BusySlotCount", result.Length)
            .Debug("Read shadow slots for peer");

        return Task.FromResult<IReadOnlyList<BusySlot>>(result);
    }

    public Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var allSlots = _slotsByPeer.Values
            .SelectMany(slots => slots)
            .Where(s => s.Start >= from && s.End <= to)
            .ToArray();

        _logger.ForContext("PeerCount", _slotsByPeer.Count)
            .ForContext("BusySlotCount", allSlots.Length)
            .Debug("Read all shadow slots from all peers");

        return Task.FromResult<IReadOnlyList<BusySlot>>(allSlots);
    }
}
