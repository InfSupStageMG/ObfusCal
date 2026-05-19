using System.ComponentModel.DataAnnotations;

namespace ObfusCal.Infrastructure.Persistence;

public class CalendarOwnerAvailabilitySlot
{
    public Guid Id { get; set; }
    public Guid CalendarOwnerId { get; set; }
    [MaxLength(512)]
    public required string SourceEventId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    [MaxLength(1024)]
    public string? Title { get; set; }
    [MaxLength(4096)]
    public string? Description { get; set; }
    public string[]? AttendeeEmails { get; set; }
    [MaxLength(1024)]
    public string? Location { get; set; }
    /// <summary>
    /// JSON array of merged source slots. Each object contains Start, End, Title, Description, AttendeeEmails, Location.
    /// Populated by MergeBlocksTransformer when adjacent/overlapping slots are merged.
    /// </summary>
    public string? SourceSlotsJson { get; set; }
}



