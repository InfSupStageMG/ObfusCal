using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

public class PeerConnection
{
    public Guid Id { get; set; }
    public required string InstanceId { get; set; }
    public required string BaseAddress { get; set; }
    public string? PinnedCertificateThumbprint { get; set; }
    public string? ClientCertificateThumbprint { get; set; }
    public string ApiKeyHash { get; set; } = string.Empty;
    public string Scopes { get; set; } = PeerApiScopes.DefaultSerializedScopes;
    public DateTimeOffset? RevokedAt { get; set; }
    public PeerConnectionStatus Status { get; set; } = PeerConnectionStatus.Active;

    public string? ClientOrganisationName { get; set; }
    public string? ClientOrganisationNameNormalized { get; set; }

    public Guid? RequestedByCalendarOwnerId { get; set; }
    public CalendarOwner? RequestedByCalendarOwner { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public bool? LastSyncSucceeded { get; set; }

    public ICollection<CalendarOwnerPeerMapping> CalendarOwnerMappings { get; set; } = [];
}
