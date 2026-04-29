using System.Collections.Concurrent;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using Serilog;

namespace ObfusCal.Infrastructure.Storage;

public sealed class InMemoryShadowSlotStore(ILogger logger) : IShadowSlotStore
{
    private const string PeerIdLogProperty = "PeerId";
    private const string CalendarOwnerIdLogProperty = "CalendarOwnerId";
    private const string PeerCountLogProperty = "PeerCount";
    private const string BusySlotCountLogProperty = "BusySlotCount";

    private readonly ConcurrentDictionary<string, IReadOnlyList<BusySlot>> _slotsByPeer = new();
    private readonly ConcurrentDictionary<(string PeerId, Guid CalendarOwnerId), IReadOnlyList<BusySlot>> _slotsByPeerAndOwner = new();
    private readonly ILogger _logger = logger.ForContext<InMemoryShadowSlotStore>();

    public Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(slots);
        ct.ThrowIfCancellationRequested();

        _slotsByPeer[peerId] = slots.ToArray();

        _logger.ForContext(PeerIdLogProperty, peerId)
            .ForContext(BusySlotCountLogProperty, slots.Count)
            .Information("Stored shadow slots for peer");

        return Task.CompletedTask;
    }

    public Task SetSlotsAsync(
        string peerId,
        Guid calendarOwnerId,
        IReadOnlyList<BusySlot> slots,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(slots);
        ct.ThrowIfCancellationRequested();

        _slotsByPeerAndOwner[(peerId, calendarOwnerId)] = slots.ToArray();

        _logger.ForContext(PeerIdLogProperty, peerId)
            .ForContext(CalendarOwnerIdLogProperty, calendarOwnerId)
            .ForContext(BusySlotCountLogProperty, slots.Count)
            .Information("Stored owner-scoped shadow slots for peer");

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ct.ThrowIfCancellationRequested();

        var result = _slotsByPeer.TryGetValue(peerId, out var slots) ? slots.ToArray() :[];

        _logger.ForContext(PeerIdLogProperty, peerId)
            .ForContext(BusySlotCountLogProperty, result.Length)
            .Debug("Read shadow slots for peer");

        return Task.FromResult<IReadOnlyList<BusySlot>>(result);
    }

    public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, Guid calendarOwnerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ct.ThrowIfCancellationRequested();

        var result = _slotsByPeerAndOwner.TryGetValue((peerId, calendarOwnerId), out var slots) ? slots.ToArray() : [];

        _logger.ForContext(PeerIdLogProperty, peerId)
            .ForContext(CalendarOwnerIdLogProperty, calendarOwnerId)
            .ForContext(BusySlotCountLogProperty, result.Length)
            .Debug("Read owner-scoped shadow slots for peer");

        return Task.FromResult<IReadOnlyList<BusySlot>>(result);
    }

    public Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var allSlots = _slotsByPeer.Values
            .SelectMany(slots => slots)
            .Where(s => s.Start < to && s.End > from)
            .ToArray();

        _logger.ForContext(PeerCountLogProperty, _slotsByPeer.Count)
            .ForContext(BusySlotCountLogProperty, allSlots.Length)
            .Debug("Read all shadow slots from all peers");

        return Task.FromResult<IReadOnlyList<BusySlot>>(allSlots);
    }

    public Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var allSlots = _slotsByPeerAndOwner
            .Where(entry => entry.Key.CalendarOwnerId == calendarOwnerId)
            .SelectMany(entry => entry.Value)
            .Where(s => s.Start < to && s.End > from)
            .ToArray();

        _logger.ForContext(CalendarOwnerIdLogProperty, calendarOwnerId)
            .ForContext(BusySlotCountLogProperty, allSlots.Length)
            .Debug("Read owner-scoped shadow slots from all peers");

        return Task.FromResult<IReadOnlyList<BusySlot>>(allSlots);
    }
}
