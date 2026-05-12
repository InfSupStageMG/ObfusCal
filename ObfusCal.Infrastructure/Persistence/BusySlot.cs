namespace ObfusCal.Infrastructure.Persistence;

/// <summary>EF Core entity for persisted busy slots. Not to be confused with <see cref="ObfusCal.Domain.Models.BusySlot"/>.</summary>
public class BusySlot
{
    public Guid Id { get; set; }
    public required string PeerId { get; set; }
    public Guid? CalendarOwnerId { get; set; }
    public required string SourceEventId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string[]? AttendeeEmails { get; set; }
    public string? Location { get; set; }

    /// <summary>
    /// UTC timestamp of when this shadow slot row was created.
    /// Used by the retention background service to purge rows older than
    /// <c>SyncOptions.ShadowSlotRetentionDays</c>.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}

