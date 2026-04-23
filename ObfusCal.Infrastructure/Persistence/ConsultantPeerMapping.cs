namespace ObfusCal.Infrastructure.Persistence;

public class ConsultantPeerMapping
{
    public Guid Id { get; set; }

    public Guid ConsultantId { get; set; }
    public Consultant Consultant { get; set; } = null!;

    public Guid PeerConnectionId { get; set; }
    public PeerConnection PeerConnection { get; set; } = null!;
}

