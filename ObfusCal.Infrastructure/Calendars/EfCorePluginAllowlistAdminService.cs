using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class EfCorePluginAllowlistAdminService(
    AppDbContext db,
    PluginAllowlistCache cache) : IPluginAllowlistAdminService
{
    public async Task<IReadOnlyList<PluginAllowlistEntry>> ListEntriesAsync(CancellationToken ct = default)
    {
        return await db.PluginAllowlistOverrides
            .OrderBy(o => o.PluginId)
            .Select(o => new PluginAllowlistEntry(o.PluginId, o.IsEnabled, o.UpdatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task SetEnabledAsync(string pluginId, bool isEnabled, CancellationToken ct = default)
    {
        var normalized = pluginId.Trim().ToLowerInvariant();

        var existing = await db.PluginAllowlistOverrides.FindAsync([normalized], ct);
        if (existing is null)
        {
            db.PluginAllowlistOverrides.Add(new PluginAllowlistOverride
            {
                PluginId = normalized,
                IsEnabled = isEnabled,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.IsEnabled = isEnabled;
            existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        // Keep in-memory cache in sync so the change is reflected immediately.
        if (isEnabled)
            cache.MarkAllowed(normalized);
        else
            cache.MarkBlocked(normalized);
    }

    public async Task RemoveOverrideAsync(string pluginId, CancellationToken ct = default)
    {
        var normalized = pluginId.Trim().ToLowerInvariant();

        var existing = await db.PluginAllowlistOverrides.FindAsync([normalized], ct);
        if (existing is not null)
        {
            db.PluginAllowlistOverrides.Remove(existing);
            await db.SaveChangesAsync(ct);
        }

        // No override means enabled by default.
        cache.MarkAllowed(normalized);
    }

    public async Task<IReadOnlySet<string>> GetBlockedPluginIdsAsync(CancellationToken ct = default)
    {
        var ids = await db.PluginAllowlistOverrides
            .Where(o => !o.IsEnabled)
            .Select(o => o.PluginId)
            .ToListAsync(ct);

        return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
    }
}

