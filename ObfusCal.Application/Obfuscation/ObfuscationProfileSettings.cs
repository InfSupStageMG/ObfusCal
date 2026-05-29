namespace ObfusCal.Application.Obfuscation;

public sealed record ObfuscationProfileSettings(
    ObfuscationAuditContext Context,
    bool RemoveTitle,
    bool RemoveDescription,
    bool RemoveLocation,
    bool RemoveAttendees,
    bool RoundTimes,
    int RoundingIntervalMinutes,
    bool MergeBlocks,
    bool RemoveSourceLabel)
{
    public static ObfuscationProfileSettings CreateDefault(ObfuscationAuditContext context) =>
        new(
            context,
            RemoveTitle: true,
            RemoveDescription: true,
            RemoveLocation: true,
            RemoveAttendees: true,
            RoundTimes: true,
            RoundingIntervalMinutes: 15,
            MergeBlocks: true,
            RemoveSourceLabel: context == ObfuscationAuditContext.Client);
}
