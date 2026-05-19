namespace ObfusCal.Application.Configuration;

/// <summary>Startup allowlist settings for external plugin loading.</summary>
public sealed class PluginAllowlistOptions
{
    public const string SectionName = "PluginAllowlist";

    public bool Enabled { get; set; } = true;

    public List<string> AllowedPluginIds { get; set; } =
    [
        "graph",
        "ical",
        "mock",
        "google",
        "icloud"
    ];

    public List<string> AllowedPublicKeyTokens { get; set; } = [];
}


