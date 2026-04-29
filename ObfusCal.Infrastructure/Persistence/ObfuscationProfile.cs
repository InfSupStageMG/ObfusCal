using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Infrastructure.Persistence;

public class ObfuscationProfile
{
    public Guid Id { get; set; }

    public Guid CalendarOwnerId { get; set; }
    public CalendarOwner CalendarOwner { get; set; } = null!;

    public ObfuscationAuditContext Context { get; set; }
    public bool RemoveTitle { get; set; } = true;
    public bool RemoveDescription { get; set; } = true;
    public bool RemoveLocation { get; set; } = true;
    public bool RemoveAttendees { get; set; } = true;
    public bool RoundTimes { get; set; } = true;
    public int RoundingIntervalMinutes { get; set; } = 15;
    public bool MergeBlocks { get; set; } = true;
}

