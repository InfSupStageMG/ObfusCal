namespace ObfusCal.Application.Interfaces;

public interface IPeerConnectionService
{
    Task<IReadOnlyList<PeerConnectionSummary>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PeerSyncStatus>> ListSyncStatusAsync(CancellationToken ct = default);
    Task<PeerConnectionSummary> CreateAsync(string instanceId, string baseAddress, string apiKey, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<LinkOwnerToPeerResult> LinkOwnerToPeerAsync(Guid calendarOwnerId, Guid peerConnectionId, CancellationToken ct = default);
}

public sealed record PeerConnectionSummary(Guid Id, string InstanceId, string BaseAddress, int MappingCount);
public sealed record PeerSyncStatus(string InstanceId, string BaseAddress, DateTimeOffset? LastSyncedAt, bool? LastSyncSucceeded);

public enum LinkOwnerToPeerOutcome
{
    Linked,
    AlreadyLinked,
    OwnerNotFound,
    PeerNotFound
}

public sealed record LinkOwnerToPeerResult(LinkOwnerToPeerOutcome Outcome, Guid? CalendarOwnerRef = null);

