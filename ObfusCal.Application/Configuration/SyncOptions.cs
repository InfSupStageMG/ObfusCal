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
    public int PeerRequestRateLimitPermitLimit { get; init; } = 240;
    public int PeerRequestRateLimitWindowSeconds { get; init; } = 60;
    public int PushShadowSlotsRateLimitPermitLimit { get; init; } = 60;
    public int PushShadowSlotsRateLimitWindowSeconds { get; init; } = 60;
    public int PullBusySlotsRateLimitPermitLimit { get; init; } = 120;
    public int PullBusySlotsRateLimitWindowSeconds { get; init; } = 60;
    public long MaxRequestBodySizeBytes { get; init; } = 1_048_576;
    public List<string> KnownPeerIds { get; init; } = [];

    /// <summary>
    /// How many days shadow slot rows (busy slots received from peers) are retained before being purged.
    /// The purge background job removes rows with <c>CreatedAtUtc</c> older than this threshold.
    /// Default: 90 days. Set to 0 to disable automatic purging.
    /// </summary>
    public int ShadowSlotRetentionDays { get; init; } = 90;
}

