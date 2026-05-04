namespace ObfusCal.Application.Interfaces;

public interface IStatusService
{
    Task<IReadOnlyList<CalendarOwnerStatusEntry>> GetStatusAsync(CancellationToken ct = default);
}

public sealed record CalendarOwnerStatusEntry(
    Guid CalendarOwnerId,
    string DisplayName,
    bool HasGraphConsent,
    DateTimeOffset? LastSyncedAt,
    bool? LastSyncSucceeded,
    IReadOnlyList<PeerConnectionStatusEntry> PeerConnections);

public sealed record PeerConnectionStatusEntry(
    Guid PeerId,
    string InstanceId,
    bool? LastSyncSucceeded,
    DateTimeOffset? LastSyncedAt);

