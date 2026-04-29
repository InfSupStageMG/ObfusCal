using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using Serilog;
using CoreBusySlot = ObfusCal.Domain.Models.BusySlot;
using DbBusySlot = ObfusCal.Infrastructure.Persistence.BusySlot;

namespace ObfusCal.Infrastructure.Storage;

public sealed class EfCoreShadowSlotStore(AppDbContext dbContext, ILogger logger) : IShadowSlotStore
{
    private readonly ILogger _logger = logger.ForContext<EfCoreShadowSlotStore>();

    public async Task SetSlotsAsync(string peerId, IReadOnlyList<CoreBusySlot> slots, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);
        ArgumentNullException.ThrowIfNull(slots);

        var existing = await dbContext.BusySlots.Where(b => b.PeerId == peerId).ToListAsync(ct);
        dbContext.BusySlots.RemoveRange(existing);

        var entities = slots.Select(s => new DbBusySlot
        {
            Id = Guid.NewGuid(),
            PeerId = peerId,
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

        _logger.ForContext("PeerId", peerId)
            .ForContext("BusySlotCount", slots.Count)
            .Information("Stored shadow slots for peer");
    }

    public async Task<IReadOnlyList<CoreBusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerId);

        var entities = await dbContext.BusySlots.Where(b => b.PeerId == peerId).ToListAsync(ct);
        var result = entities.Select(e => new CoreBusySlot(
            e.SourceEventId,
            e.Start,
            e.End,
            e.Title,
            e.Description,
            e.AttendeeEmails,
            e.Location)).ToArray();

        _logger.ForContext("PeerId", peerId)
            .ForContext("BusySlotCount", result.Length)
            .Debug("Read shadow slots for peer");

        return result;
    }

    public async Task<IReadOnlyList<CoreBusySlot>> GetAllSlotsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var entities = await dbContext.BusySlots.Where(b => b.Start >= from && b.End <= to).ToListAsync(ct);
        var result = entities.Select(e => new CoreBusySlot(
            e.SourceEventId,
            e.Start,
            e.End,
            e.Title,
            e.Description,
            e.AttendeeEmails,
            e.Location)).ToArray();

        _logger.ForContext("BusySlotCount", result.Length)
            .Debug("Read all shadow slots from all peers");

        return result;
    }
}
