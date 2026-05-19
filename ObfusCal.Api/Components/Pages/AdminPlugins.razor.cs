using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace ObfusCal.Api.Components.Pages;

public partial class AdminPlugins : ComponentBase
{
    private bool _isSysadmin;
    private bool _loading = true;
    private List<PluginRow>? _rows;
    private string? _statusMessage;
    private MessageIntent _statusIntent = MessageIntent.Info;

    protected override async Task OnInitializedAsync()
    {
        var currentUser = await CurrentUserContextAccessor.GetCurrentAsync();
        _isSysadmin = currentUser.IsSysadmin;

        if (!_isSysadmin)
        {
            _loading = false;
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;

        var overrides = (await AllowlistService.ListEntriesAsync())
            .ToDictionary(e => e.PluginId, StringComparer.OrdinalIgnoreCase);

        var allStartupPlugins = Catalog.GetPlugins().ToList();

        var blockedIds = overrides.Where(kvp => !kvp.Value.IsEnabled).Select(kvp => kvp.Key);

        _rows = allStartupPlugins
            .Select(plugin =>
            {
                overrides.TryGetValue(plugin.Id, out var entry);
                return new PluginRow(
                    plugin.Id,
                    plugin.DisplayName,
                    plugin.IsExternalPlugin,
                    IsEnabled: entry is null || entry.IsEnabled,
                    HasOverride: entry is not null,
                    OverrideUpdatedAtUtc: entry?.UpdatedAtUtc);
            })
            .ToList();

        foreach (var id in blockedIds)
        {
            var entry = overrides[id];
            _rows.Add(new PluginRow(
                entry.PluginId,
                entry.PluginId,
                IsExternal: true,
                IsEnabled: false,
                HasOverride: true,
                OverrideUpdatedAtUtc: entry.UpdatedAtUtc));
        }

        _rows = [.. _rows.OrderBy(r => r.PluginId)];
        _loading = false;
    }

    private async Task SetEnabledAsync(string pluginId, bool isEnabled)
    {
        _statusMessage = null;
        try
        {
            await AllowlistService.SetEnabledAsync(pluginId, isEnabled);
            _statusMessage = isEnabled
                ? $"Plugin '{pluginId}' has been enabled."
                : $"Plugin '{pluginId}' has been disabled. Existing active sessions are not affected.";
            _statusIntent = MessageIntent.Success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _statusMessage = $"Failed to update plugin '{pluginId}'.";
            _statusIntent = MessageIntent.Error;
        }
        await LoadAsync();
    }

    private async Task RemoveOverrideAsync(string pluginId)
    {
        _statusMessage = null;
        try
        {
            await AllowlistService.RemoveOverrideAsync(pluginId);
            _statusMessage = $"Override for plugin '{pluginId}' has been removed; it reverts to its startup default.";
            _statusIntent = MessageIntent.Success;
        }
        catch (InvalidOperationException)
        {
            _statusMessage = $"Failed to remove override for plugin '{pluginId}'.";
            _statusIntent = MessageIntent.Error;
        }
        await LoadAsync();
    }

    internal sealed record PluginRow(
        string PluginId,
        string DisplayName,
        bool IsExternal,
        bool IsEnabled,
        bool HasOverride,
        DateTimeOffset? OverrideUpdatedAtUtc);
}


