using System.Collections.Immutable;

namespace ObfusCal.Infrastructure.Calendars;

/// <summary>In-memory set of runtime-blocked plugin IDs.</summary>
internal sealed class PluginAllowlistCache
{
    private volatile ImmutableHashSet<string> _blocked = ImmutableHashSet<string>.Empty;
    private volatile bool _initialized;

    public bool IsInitialized => _initialized;

    public IReadOnlySet<string> GetBlockedPluginIds() => _blocked;

    public void Initialize(IEnumerable<string> blockedPluginIds)
    {
        _blocked = ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, blockedPluginIds);
        _initialized = true;
    }

    public void MarkBlocked(string pluginId) =>
        _blocked = _blocked.Add(pluginId.Trim().ToLowerInvariant());

    public void MarkAllowed(string pluginId) =>
        _blocked = _blocked.Remove(pluginId.Trim().ToLowerInvariant());
}


