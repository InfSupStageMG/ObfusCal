namespace ObfusCal.Infrastructure.Persistence;

/// <summary>Persisted runtime allow/deny override per plugin ID.</summary>
public sealed class PluginAllowlistOverride
{
    public required string PluginId { get; set; }

    public required bool IsEnabled { get; set; }

    public required DateTimeOffset UpdatedAtUtc { get; set; }
}



