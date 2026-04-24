namespace ObfusCal.Application.Configuration;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    public List<string> KnownPeerIds { get; init; } = [];
}

