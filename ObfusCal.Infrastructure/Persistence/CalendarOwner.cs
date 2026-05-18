namespace ObfusCal.Infrastructure.Persistence;

public class CalendarOwner
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? EntraObjectId { get; set; }
    public string? CalendarSourcePluginId { get; set; }
    public string? ICloudCalendarUrl { get; set; }
    public string? ICloudAppleIdProtected { get; set; }
    public string? ICloudAppSpecificPasswordProtected { get; set; }
    public string? GraphAccessTokenProtected { get; set; }
    public string? GraphRefreshTokenProtected { get; set; }
    public DateTimeOffset? GraphTokenExpiresAtUtc { get; set; }
    public DateTimeOffset? GraphTokenLastRefreshedAtUtc { get; set; }
    public DateTimeOffset? GraphConsentGrantedAtUtc { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public bool? LastSyncSucceeded { get; set; }
    public bool WriteBackEnabled { get; set; }
    public string? WriteBackPlaceholderTitle { get; set; }
    public ICollection<CalendarOwnerPeerMapping> PeerMappings { get; set; } = [];
    public ICollection<ObfuscationProfile> ObfuscationProfiles { get; set; } = [];
    public ICollection<CalendarSourceInstance> CalendarSourceInstances { get; set; } = [];

    public ICollection<CalendarOwnerICalFeed> ICalFeeds { get; set; } = [];
}
