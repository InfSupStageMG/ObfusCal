using ObfusCal.Core.Models;

namespace ObfusCal.Core.Configuration;

public class SyncOptions
{
    public const string Section = "Sync";

    public string InstanceId { get; set; } = "A";
    public List<PeerInfo> Peers { get; set; } = [];
    public int SyncIntervalSeconds { get; set; } = 60;
    public int LookAheadDays { get; set; } = 14;
}