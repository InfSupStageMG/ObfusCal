using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed persistence for the last completed peer sync timestamp.
/// </summary>
public sealed class EfCorePeerSyncHistoryStore(AppDbContext dbContext) : IPeerSyncHistoryStore
{
    public async Task<DateTimeOffset?> GetLastCompletedAtUtcAsync(CancellationToken ct = default)
    {
        return await dbContext.Set<PeerSyncState>()
            .Where(state => state.Id == PeerSyncState.SingletonId)
            .Select(state => state.LastCompletedAtUtc)
            .SingleOrDefaultAsync(ct);
    }

    public async Task SetLastCompletedAtUtcAsync(DateTimeOffset completedAtUtc, CancellationToken ct = default)
    {
        var normalizedCompletedAtUtc = completedAtUtc.ToUniversalTime();
        var state = await dbContext.Set<PeerSyncState>()
            .SingleOrDefaultAsync(existing => existing.Id == PeerSyncState.SingletonId, ct);

        if (state is null)
        {
            state = new PeerSyncState
            {
                Id = PeerSyncState.SingletonId,
                LastCompletedAtUtc = normalizedCompletedAtUtc
            };
            dbContext.Set<PeerSyncState>().Add(state);
        }
        else if (state.LastCompletedAtUtc is null || normalizedCompletedAtUtc > state.LastCompletedAtUtc.Value)
        {
            state.LastCompletedAtUtc = normalizedCompletedAtUtc;
        }

        await dbContext.SaveChangesAsync(ct);
    }
}

