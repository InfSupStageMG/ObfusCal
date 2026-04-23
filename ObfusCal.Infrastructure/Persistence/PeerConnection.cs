namespace ObfusCal.Infrastructure.Persistence;

public class PeerConnection
{
    public Guid Id { get; set; }
    public required string InstanceId { get; set; }
    public required string BaseAddress { get; set; }

    public ICollection<CalendarOwnerPeerMapping> CalendarOwnerMappings { get; set; } = [];
}

