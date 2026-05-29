namespace ObfusCal.Infrastructure.Persistence;

/// <summary>
/// Stores singleton metadata for peer sync runtime state that must survive app restarts.
/// </summary>
public sealed class PeerSyncState
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public DateTimeOffset? LastCompletedAtUtc { get; set; }
}

