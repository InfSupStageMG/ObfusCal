using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class PeerConnectionService(AppDbContext dbContext) : IPeerConnectionService
{
    public async Task<IReadOnlyList<PeerConnectionSummary>> ListAsync(CancellationToken ct = default)
    {
        return await dbContext.PeerConnections
            .AsNoTracking()
            .OrderBy(p => p.InstanceId)
            .Select(p => new PeerConnectionSummary(
                p.Id,
                p.InstanceId,
                p.BaseAddress,
                p.CalendarOwnerMappings.Count))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PeerSyncStatus>> ListSyncStatusAsync(CancellationToken ct = default)
    {
        return await dbContext.PeerConnections
            .AsNoTracking()
            .OrderBy(p => p.InstanceId)
            .Select(p => new PeerSyncStatus(
                p.InstanceId,
                p.BaseAddress,
                p.LastSyncedAt,
                p.LastSyncSucceeded))
            .ToListAsync(ct);
    }

    public async Task<PeerConnectionSummary> CreateAsync(string instanceId, string baseAddress, string apiKey, CancellationToken ct = default)
    {
        var peer = new PeerConnection
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId.Trim(),
            BaseAddress = baseAddress.Trim().TrimEnd('/'),
            ApiKeyHash = apiKey.Trim() // In production this should be hashed
        };

        dbContext.PeerConnections.Add(peer);
        await dbContext.SaveChangesAsync(ct);

        return new PeerConnectionSummary(peer.Id, peer.InstanceId, peer.BaseAddress, 0);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var peer = await dbContext.PeerConnections.FindAsync([id], ct);
        if (peer is null) return false;

        dbContext.PeerConnections.Remove(peer);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<LinkOwnerToPeerResult> LinkOwnerToPeerAsync(Guid calendarOwnerId, Guid peerConnectionId, CancellationToken ct = default)
    {
        var ownerExists = await dbContext.CalendarOwners.AnyAsync(o => o.Id == calendarOwnerId, ct);
        if (!ownerExists) return new LinkOwnerToPeerResult(LinkOwnerToPeerOutcome.OwnerNotFound);

        var peerExists = await dbContext.PeerConnections.AnyAsync(p => p.Id == peerConnectionId, ct);
        if (!peerExists) return new LinkOwnerToPeerResult(LinkOwnerToPeerOutcome.PeerNotFound);

        var alreadyLinked = await dbContext.CalendarOwnerPeerMappings
            .AnyAsync(m => m.CalendarOwnerId == calendarOwnerId && m.PeerConnectionId == peerConnectionId, ct);
        if (alreadyLinked) return new LinkOwnerToPeerResult(LinkOwnerToPeerOutcome.AlreadyLinked);

        var mapping = new CalendarOwnerPeerMapping
        {
            Id = Guid.NewGuid(),
            CalendarOwnerId = calendarOwnerId,
            PeerConnectionId = peerConnectionId,
            CalendarOwnerRef = Guid.NewGuid()
        };

        dbContext.CalendarOwnerPeerMappings.Add(mapping);
        await dbContext.SaveChangesAsync(ct);

        return new LinkOwnerToPeerResult(LinkOwnerToPeerOutcome.Linked, mapping.CalendarOwnerRef);
    }
}

