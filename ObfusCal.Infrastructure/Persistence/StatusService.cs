using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class StatusService(AppDbContext dbContext) : IStatusService
{
    public async Task<IReadOnlyList<CalendarOwnerStatusEntry>> GetStatusAsync(CancellationToken ct = default)
    {
        var owners = await dbContext.CalendarOwners
            .AsNoTracking()
            .Include(o => o.PeerMappings)
                .ThenInclude(m => m.PeerConnection)
            .OrderBy(o => o.Name)
            .ToListAsync(ct);

        return owners.Select(owner => new CalendarOwnerStatusEntry(
            owner.Id,
            owner.Name,
            owner.GraphConsentGrantedAtUtc is not null,
            owner.LastSyncedAt,
            owner.LastSyncSucceeded,
            owner.PeerMappings.Select(m => new PeerConnectionStatusEntry(
                m.PeerConnectionId,
                m.PeerConnection.InstanceId,
                m.PeerConnection.LastSyncSucceeded,
                m.PeerConnection.LastSyncedAt)).ToList())).ToList();
    }
}

