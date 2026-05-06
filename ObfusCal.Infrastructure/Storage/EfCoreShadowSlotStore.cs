using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using Serilog;
using CoreBusySlot = ObfusCal.Domain.Models.BusySlot;
using DbBusySlot = ObfusCal.Infrastructure.Persistence.BusySlot;

namespace ObfusCal.Infrastructure.Storage;

public sealed class EfCoreShadowSlotStore(AppDbContext dbContext, ILogger logger) : IShadowSlotStore
{
    private const string PeerIdLogProperty = "PeerId";
    private const string CalendarOwnerIdLogProperty = "CalendarOwnerId";
    private const string BusySlotCountLogProperty = "BusySlotCount";

    private readonly ILogger _logger = logger.ForContext<EfCoreShadowSlotStore>();

    public async Task SetSlotsAsync(string peerId, IReadOnlyList<CoreBusySlot> slots, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(slots);

        var existing = await dbContext.BusySlots
            .Where(b => b.PeerId == peerId && b.CalendarOwnerId == null)
            .ToListAsync(ct);
        dbContext.BusySlots.RemoveRange(existing);

        var entities = slots.Select(s => new DbBusySlot
        {
            Id = Guid.NewGuid(),
            PeerId = peerId,
            CalendarOwnerId = null,
            SourceEventId = s.SourceEventId,
            Start = s.Start,
            End = s.End,
            Title = s.Title,
            Description = s.Description,
            AttendeeEmails = s.AttendeeEmails?.ToArray(),
            Location = s.Location
        }).ToList();

        await dbContext.BusySlots.AddRangeAsync(entities, ct);
        await dbContext.SaveChangesAsync(ct);

        _logger.ForContext(PeerIdLogProperty, peerId)
            .ForContext(BusySlotCountLogProperty, slots.Count)
            .Information("Stored shadow slots for peer");
    }

    public async Task SetSlotsAsync(
        string peerId,
        Guid calendarOwnerId,
        IReadOnlyList<CoreBusySlot> slots,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(slots);

        var existing = await dbContext.BusySlots
            .Where(b => b.PeerId == peerId && b.CalendarOwnerId == calendarOwnerId)
            .ToListAsync(ct);
        dbContext.BusySlots.RemoveRange(existing);

        var entities = slots.Select(s => new DbBusySlot
        {
            Id = Guid.NewGuid(),
            PeerId = peerId,
            CalendarOwnerId = calendarOwnerId,
            SourceEventId = s.SourceEventId,
            Start = s.Start,
            End = s.End
        }).ToList();

        await dbContext.BusySlots.AddRangeAsync(entities, ct);
        await dbContext.SaveChangesAsync(ct);

        _logger.ForContext(PeerIdLogProperty, peerId)
            .ForContext(CalendarOwnerIdLogProperty, calendarOwnerId)
            .ForContext(BusySlotCountLogProperty, slots.Count)
            .Information("Stored owner-scoped shadow slots for peer");
    }

    public async Task<IReadOnlyList<CoreBusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);

        var entities = await dbContext.BusySlots
            .AsNoTracking()
            .Where(b => b.PeerId == peerId && b.CalendarOwnerId == null)
            .ToListAsync(ct);
        var result = entities.Select(e => new CoreBusySlot(
            e.SourceEventId,
            e.Start,
            e.End,
            e.Title,
            e.Description,
            e.AttendeeEmails,
            e.Location)).ToArray();

        _logger.ForContext(PeerIdLogProperty, peerId)
            .ForContext(BusySlotCountLogProperty, result.Length)
            .Debug("Read shadow slots for peer");

        return result;
    }

    public async Task<IReadOnlyList<CoreBusySlot>> GetSlotsAsync(
        string peerId,
        Guid calendarOwnerId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);

        var entities = await dbContext.BusySlots
            .AsNoTracking()
            .Where(b => b.PeerId == peerId && b.CalendarOwnerId == calendarOwnerId)
            .ToListAsync(ct);
        var result = entities.Select(e => new CoreBusySlot(e.SourceEventId, e.Start, e.End)).ToArray();

        _logger.ForContext(PeerIdLogProperty, peerId)
            .ForContext(CalendarOwnerIdLogProperty, calendarOwnerId)
            .ForContext(BusySlotCountLogProperty, result.Length)
            .Debug("Read owner-scoped shadow slots for peer");

        return result;
    }

    public async Task<IReadOnlyList<CoreBusySlot>> GetAllSlotsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var entities = await dbContext.BusySlots
            .AsNoTracking()
            .Where(b => b.CalendarOwnerId == null)
            .Where(b => b.Start < to && b.End > from)
            .ToListAsync(ct);
        var result = entities.Select(e => new CoreBusySlot(
            e.SourceEventId,
            e.Start,
            e.End,
            e.Title,
            e.Description,
            e.AttendeeEmails,
            e.Location)).ToArray();

        _logger.ForContext(BusySlotCountLogProperty, result.Length)
            .Debug("Read all shadow slots from all peers");

        return result;
    }

    public async Task<IReadOnlyList<CoreBusySlot>> GetAllSlotsAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var activePeerIds = await dbContext.CalendarOwnerPeerMappings
            .AsNoTracking()
            .Where(mapping => mapping.CalendarOwnerId == calendarOwnerId)
            .Where(mapping => mapping.PeerConnection.Status == PeerConnectionStatus.Active)
            .Select(mapping => mapping.PeerConnection.InstanceId)
            .Distinct()
            .ToListAsync(ct);

        if (activePeerIds.Count == 0)
        {
            _logger.ForContext(CalendarOwnerIdLogProperty, calendarOwnerId)
                .ForContext(BusySlotCountLogProperty, 0)
                .Debug("Read owner-scoped shadow slots from all peers");

            return [];
        }

        var entities = await dbContext.BusySlots
            .AsNoTracking()
            .Where(b => b.CalendarOwnerId == calendarOwnerId)
            .Where(b => activePeerIds.Contains(b.PeerId))
            .Where(b => b.Start < to && b.End > from)
            .ToListAsync(ct);
        var result = entities.Select(e => new CoreBusySlot(e.SourceEventId, e.Start, e.End)).ToArray();

        _logger.ForContext(CalendarOwnerIdLogProperty, calendarOwnerId)
            .ForContext(BusySlotCountLogProperty, result.Length)
            .Debug("Read owner-scoped shadow slots from all peers");

        return result;
    }
}
