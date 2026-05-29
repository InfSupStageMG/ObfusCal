namespace ObfusCal.Application.Interfaces;

/// <summary>
/// Persists and retrieves the timestamp of the last completed peer sync cycle.
/// </summary>
public interface IPeerSyncHistoryStore
{
    Task<DateTimeOffset?> GetLastCompletedAtUtcAsync(CancellationToken ct = default);
    Task SetLastCompletedAtUtcAsync(DateTimeOffset completedAtUtc, CancellationToken ct = default);
}

