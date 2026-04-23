namespace ObfusCal.Infrastructure.Persistence;

public class CalendarOwnerPeerMapping
{
    public Guid Id { get; set; }

    public Guid CalendarOwnerId { get; set; }
    public CalendarOwner CalendarOwner { get; set; } = null!;

    public Guid PeerConnectionId { get; set; }
    public PeerConnection PeerConnection { get; set; } = null!;
    public Guid CalendarOwnerRef { get; set; }
}

