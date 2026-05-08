namespace ObfusCal.Application.Configuration;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    public int SyncIntervalSeconds { get; init; } = 900;
    public int LookAheadDays { get; init; } = 14;
    // Subject to change - potentially make configurable by sysadmin
    public int MaxQueryWindowDays { get; init; } = 90;
    // Subject to change - potentially make configurable by sysadmin
    public int MaxShadowSlotsPerRequest { get; init; } = 500;
    public string InstanceId { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public int PeerRequestTimestampToleranceSeconds { get; init; } = 300;
    public List<string> KnownPeerIds { get; init; } = [];
}

