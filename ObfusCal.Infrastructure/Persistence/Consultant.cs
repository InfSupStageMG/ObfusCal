namespace ObfusCal.Infrastructure.Persistence;

public class Consultant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public ICollection<ConsultantPeerMapping> PeerMappings { get; set; } = [];
}

