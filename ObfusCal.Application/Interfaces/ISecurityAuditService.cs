namespace ObfusCal.Application.Interfaces;

public interface ISecurityAuditService
{
    Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken ct = default);
}

public sealed record SecurityAuditEvent(
    string EventCode,
    string Outcome,
    string ActorIdentity,
    string TargetResource,
    string? TargetId,
    string CorrelationId,
    IReadOnlyDictionary<string, string?>? Metadata = null);

public static class SecurityAuditEventCodes
{
    public const string AuthSuccess = "AUTH_SUCCESS";
    public const string AuthFailure = "AUTH_FAILURE";
    public const string PeerSlotPush = "PEER_SLOT_PUSH";
    public const string PeerSlotRejected = "PEER_SLOT_REJECTED";
    public const string ConfigChange = "CONFIG_CHANGE";
    public const string KeyRotation = "KEY_ROTATION";
    public const string KeyRevocation = "KEY_REVOCATION";
    public const string StatusRead = "STATUS_READ";
}

public static class SecurityAuditOutcomes
{
    public const string Success = "SUCCESS";
    public const string Failure = "FAILURE";
}

