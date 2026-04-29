namespace ObfusCal.Infrastructure.Persistence;

public class PeerConnection
{
    public Guid Id { get; set; }
    public required string InstanceId { get; set; }
    public required string BaseAddress { get; set; }
    public string ApiKeyHash { get; set; } = string.Empty;

    /// <summary>Timestamp of the last sync attempt (outbound or inbound), whether successful or not.</summary>
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>Whether the most recent sync attempt for this peer succeeded.</summary>
    public bool? LastSyncSucceeded { get; set; }

    public ICollection<CalendarOwnerPeerMapping> CalendarOwnerMappings { get; set; } = [];
}
