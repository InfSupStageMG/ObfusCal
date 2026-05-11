using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Calendars;

public sealed class EfCorePeerCalendarOwnerResolver(AppDbContext dbContext) : IPeerCalendarOwnerResolver
{
    public async Task<IReadOnlyList<Guid>> ResolveCalendarOwnerIdsAsync(string peerId, Guid calendarOwnerRef, CancellationToken ct = default)
    {
        return await dbContext.CalendarOwnerPeerMappings
            .AsNoTracking()
            .Where(mapping => mapping.PeerConnection.InstanceId == peerId)
            .Where(mapping => mapping.PeerConnection.Status == PeerConnectionStatus.Active)
            .Where(mapping => mapping.PeerConnection.RevokedAt == null)
            .Where(mapping => mapping.CalendarOwnerRef == calendarOwnerRef)
            .Select(mapping => mapping.CalendarOwnerId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ResolveAllCalendarOwnerIdsAsync(string peerId, CancellationToken ct = default)
    {
        return await dbContext.CalendarOwnerPeerMappings
            .AsNoTracking()
            .Where(mapping => mapping.PeerConnection.InstanceId == peerId)
            .Where(mapping => mapping.PeerConnection.Status == PeerConnectionStatus.Active)
            .Where(mapping => mapping.PeerConnection.RevokedAt == null)
            .Select(mapping => mapping.CalendarOwnerId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<Guid> ResolveSingleCalendarOwnerIdAsync(string peerId, Guid calendarOwnerRef, CancellationToken ct = default)
    {
        return await dbContext.CalendarOwnerPeerMappings
            .Where(mapping => mapping.CalendarOwnerRef == calendarOwnerRef)
            .Where(mapping => mapping.PeerConnection.InstanceId == peerId)
            .Where(mapping => mapping.PeerConnection.Status == PeerConnectionStatus.Active)
            .Where(mapping => mapping.PeerConnection.RevokedAt == null)
            .Select(mapping => mapping.CalendarOwnerId)
            .SingleOrDefaultAsync(ct);
    }
}

