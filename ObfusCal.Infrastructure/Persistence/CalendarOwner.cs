namespace ObfusCal.Infrastructure.Persistence;

public class CalendarOwner
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? EntraObjectId { get; set; }
    public string? CalendarSourcePluginId { get; set; }
    public string? GraphAccessTokenProtected { get; set; }
    public string? GraphRefreshTokenProtected { get; set; }
    public DateTimeOffset? GraphTokenExpiresAtUtc { get; set; }
    public DateTimeOffset? GraphTokenLastRefreshedAtUtc { get; set; }
    public DateTimeOffset? GraphConsentGrantedAtUtc { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public bool? LastSyncSucceeded { get; set; }

    public ICollection<CalendarOwnerPeerMapping> PeerMappings { get; set; } = [];
    public ICollection<ObfuscationProfile> ObfuscationProfiles { get; set; } = [];

    /// <summary>
    /// External iCal feed sources configured for this owner.
    /// Each entry represents a calendar from a peer organisation that does not run ObfusCal.
    /// </summary>
    public ICollection<CalendarOwnerICalFeed> ICalFeeds { get; set; } = [];
}
