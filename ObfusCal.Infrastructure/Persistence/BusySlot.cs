namespace ObfusCal.Infrastructure.Persistence;

/// <summary>EF Core entity for persisted busy slots. Not to be confused with <see cref="ObfusCal.Core.Models.BusySlot"/>.</summary>
public class BusySlot
{
    public Guid Id { get; set; }
    public required string PeerId { get; set; }
    public required string SourceEventId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string[]? AttendeeEmails { get; set; }
    public string? Location { get; set; }
}

