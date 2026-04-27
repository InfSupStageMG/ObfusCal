namespace ObfusCal.Infrastructure.Persistence;

public class CalendarOwner
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? EntraObjectId { get; set; }

    public ICollection<CalendarOwnerPeerMapping> PeerMappings { get; set; } = [];
}
