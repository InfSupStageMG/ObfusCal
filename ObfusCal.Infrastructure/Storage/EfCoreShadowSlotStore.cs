using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using Serilog;
using CoreBusySlot = ObfusCal.Domain.Models.BusySlot;
using DbBusySlot = ObfusCal.Infrastructure.Persistence.BusySlot;

namespace ObfusCal.Infrastructure.Storage;

/// <summary>
/// Entity Framework Core implementation of IShadowSlotStore using PostgreSQL.
/// </summary>
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
            Location = s.Location,
            SourceName = s.SourceName,
            IsAllDay = s.IsAllDay,
            CreatedAtUtc = DateTimeOffset.UtcNow
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
            End = s.End,
            Title = s.Title,
            Description = s.Description,
            AttendeeEmails = s.AttendeeEmails?.ToArray(),
            Location = s.Location,
            SourceName = s.SourceName,
            IsAllDay = s.IsAllDay,
            CreatedAtUtc = DateTimeOffset.UtcNow
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
            e.Location,
            IsAllDay: e.IsAllDay,
            SourceName: e.SourceName)).ToArray();

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
        var result = entities.Select(e => new CoreBusySlot(
            e.SourceEventId,
            e.Start,
            e.End,
            e.Title,
            e.Description,
            e.AttendeeEmails,
            e.Location,
            IsAllDay: e.IsAllDay,
            SourceName: e.SourceName)).ToArray();

        _logger.ForContext(PeerIdLogProperty, peerId)
            .ForContext(CalendarOwnerIdLogProperty, calendarOwnerId)
            .ForContext(BusySlotCountLogProperty, result.Length)
            .Debug("Read owner-scoped shadow slots for peer");

        return result;
    }

    public async Task<IReadOnlyList<CoreBusySlot>> GetAllSlotsAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
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
            e.Location,
            IsAllDay: e.IsAllDay,
            SourceName: e.SourceName)).ToArray();

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
        var activePeers = await dbContext.CalendarOwnerPeerMappings
            .AsNoTracking()
            .Where(m => m.CalendarOwnerId == calendarOwnerId && m.PeerConnection.Status == PeerConnectionStatus.Active)
            .Select(m => new { m.PeerConnection.InstanceId, m.PeerConnection.ClientOrganisationName })
            .Distinct()
            .ToListAsync(ct);

        if (activePeers.Count == 0) return [];

        var validPeerIds = activePeers.Select(p => p.InstanceId).ToList();

        var entities = await dbContext.BusySlots
            .AsNoTracking()
            .Where(b => b.CalendarOwnerId == calendarOwnerId && validPeerIds.Contains(b.PeerId))
            .Where(b => b.Start < to && b.End > from)
            .ToListAsync(ct);

        var peerLabels = activePeers
            .GroupBy(x => x.InstanceId)
            .ToDictionary(
                g => g.Key,
                // Prefer the human-readable organisation name from the request/approve flow;
                // fall back to the instance ID for sysadmin-created connections where no name was supplied.
                g => !string.IsNullOrWhiteSpace(g.First().ClientOrganisationName)
                    ? g.First().ClientOrganisationName
                    : g.Key);

        var result = entities.Select(e => new CoreBusySlot(
            e.SourceEventId,
            e.Start,
            e.End,
            e.Title,
            e.Description,
            e.AttendeeEmails,
            e.Location,
            IsAllDay: e.IsAllDay,
            SourceName: e.SourceName) with
        {
            SourceLabel = peerLabels.GetValueOrDefault(e.PeerId)
        }).ToArray();

        _logger.ForContext(CalendarOwnerIdLogProperty, calendarOwnerId)
            .ForContext(BusySlotCountLogProperty, result.Length)
            .Debug("Read owner-scoped shadow slots from all peers");

        return result;
    }
}
