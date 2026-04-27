namespace ObfusCal.Infrastructure.Persistence;

public class CalendarOwner
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? EntraObjectId { get; set; }
    public string? GraphAccessTokenProtected { get; set; }
    public string? GraphRefreshTokenProtected { get; set; }
    public DateTimeOffset? GraphTokenExpiresAtUtc { get; set; }
    public DateTimeOffset? GraphTokenLastRefreshedAtUtc { get; set; }
    public DateTimeOffset? GraphConsentGrantedAtUtc { get; set; }

    public ICollection<CalendarOwnerPeerMapping> PeerMappings { get; set; } = [];
}
