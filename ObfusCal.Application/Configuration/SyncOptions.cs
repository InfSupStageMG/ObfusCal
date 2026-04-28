namespace ObfusCal.Application.Configuration;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    public int SyncIntervalSeconds { get; init; } = 900;
    public int LookAheadDays { get; init; } = 14;
    public string InstanceId { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public List<string> KnownPeerIds { get; init; } = [];
}

