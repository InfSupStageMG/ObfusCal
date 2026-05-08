namespace ObfusCal.Application.Interfaces;

public interface IPeerConnectionService
{
    Task<IReadOnlyList<PeerConnectionSummary>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PeerConnectionRequestSummary>> ListForCalendarOwnerAsync(Guid calendarOwnerId, CancellationToken ct = default);
    Task<IReadOnlyList<AdminPeerConnectionSummary>> ListForAdminAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PeerSyncStatus>> ListSyncStatusAsync(CancellationToken ct = default);
    Task<PeerConnectionSummary> CreateAsync(string instanceId, string baseAddress, string apiKey, IEnumerable<string>? scopes = null, CancellationToken ct = default);
    Task<CreatePeerConnectionRequestResult> CreateRequestAsync(Guid calendarOwnerId, string clientOrganisationName, CancellationToken ct = default);
    Task<ApprovePeerConnectionResult> ApproveAsync(Guid id, string peerBaseUrl, IEnumerable<string>? scopes = null, CancellationToken ct = default);
    Task<RotatePeerApiKeyResult> RotateApiKeyAsync(Guid id, CancellationToken ct = default);
    Task<RevokePeerConnectionResult> RevokeAsync(Guid id, CancellationToken ct = default);
    Task<bool> SuspendAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<LinkOwnerToPeerResult> LinkOwnerToPeerAsync(Guid calendarOwnerId, Guid peerConnectionId, CancellationToken ct = default);
}

public sealed record PeerConnectionSummary(Guid Id, string InstanceId, string BaseAddress, int MappingCount);
public sealed record PeerConnectionRequestSummary(Guid Id, string ClientOrganisationName, PeerConnectionStatus Status);
public sealed record AdminPeerConnectionSummary(
    Guid Id,
    string InstanceId,
    string BaseAddress,
    PeerConnectionStatus Status,
    string? ClientOrganisationName,
    Guid? RequestedByCalendarOwnerId,
    string? RequestedByCalendarOwnerName,
    int MappingCount);
public sealed record PeerSyncStatus(string InstanceId, string BaseAddress, DateTimeOffset? LastSyncedAt, bool? LastSyncSucceeded);

public enum CreatePeerConnectionRequestOutcome
{
    Created,
    Duplicate,
    CalendarOwnerNotFound
}

public sealed record CreatePeerConnectionRequestResult(
    CreatePeerConnectionRequestOutcome Outcome,
    Guid? PeerConnectionId = null);

public enum ApprovePeerConnectionOutcome
{
    Approved,
    NotFound,
    AlreadyActive,
    InvalidBaseUrl
}

public sealed record ApprovePeerConnectionResult(
    ApprovePeerConnectionOutcome Outcome,
    string? PlaintextApiKey = null);

public enum RotatePeerApiKeyOutcome
{
    Rotated,
    NotFound,
    NotActive,
    Revoked
}

public sealed record RotatePeerApiKeyResult(
    RotatePeerApiKeyOutcome Outcome,
    string? PlaintextApiKey = null);

public enum RevokePeerConnectionOutcome
{
    Revoked,
    NotFound,
    AlreadyRevoked
}

public sealed record RevokePeerConnectionResult(
    RevokePeerConnectionOutcome Outcome);

public enum PeerConnectionStatus
{
    Requested,
    Active,
    Suspended
}

public enum LinkOwnerToPeerOutcome
{
    Linked,
    AlreadyLinked,
    OwnerNotFound,
    PeerNotFound
}

public sealed record LinkOwnerToPeerResult(LinkOwnerToPeerOutcome Outcome, Guid? CalendarOwnerRef = null);

