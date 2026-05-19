namespace ObfusCal.Application.Interfaces;

/// <summary>Sysadmin API for runtime plugin allowlist overrides.</summary>
public interface IPluginAllowlistAdminService
{
    Task<IReadOnlyList<PluginAllowlistEntry>> ListEntriesAsync(CancellationToken ct = default);

    Task SetEnabledAsync(string pluginId, bool isEnabled, CancellationToken ct = default);

    Task RemoveOverrideAsync(string pluginId, CancellationToken ct = default);

    Task<IReadOnlySet<string>> GetBlockedPluginIdsAsync(CancellationToken ct = default);
}

/// <summary>Persisted runtime allow/deny override for a plugin ID.</summary>
public sealed record PluginAllowlistEntry(
    string PluginId,
    bool IsEnabled,
    DateTimeOffset UpdatedAtUtc);


