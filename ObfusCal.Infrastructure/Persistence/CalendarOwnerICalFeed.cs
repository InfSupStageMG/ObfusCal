namespace ObfusCal.Infrastructure.Persistence;

/// <summary>
/// Represents a read-only iCal feed URL associated with a calendar owner.
/// A calendar owner may have multiple feeds, each representing an external calendar
/// source (e.g., a peer organisation that does not run an ObfusCal instance).
/// </summary>
public class CalendarOwnerICalFeed
{
    public Guid Id { get; set; }
    public Guid CalendarOwnerId { get; set; }
    public required string FeedUrl { get; set; }

    public CalendarOwner CalendarOwner { get; set; } = null!;
}

