using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize(Policy = AppAuthorizationPolicies.Sysadmin)]
[Route("api/admin/plugin-allowlist")]
public sealed class AdminPluginAllowlistController(
    IPluginAllowlistAdminService allowlistService,
    ICalendarSourceCatalog catalog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PluginAllowlistStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListPlugins(CancellationToken ct)
    {
        var overrides = (await allowlistService.ListEntriesAsync(ct))
            .ToDictionary(e => e.PluginId, StringComparer.OrdinalIgnoreCase);

        var catalogPlugins = catalog.GetPlugins()
            .ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

        var allPluginIds = overrides.Keys.Union(catalogPlugins.Keys, StringComparer.OrdinalIgnoreCase);

        var response = allPluginIds
            .Select(pluginId => BuildStatusResponse(pluginId, overrides, catalogPlugins))
            .ToList();

        return Ok(response);
    }

    [HttpPut("{pluginId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEnabled(
        [FromRoute, MaxLength(128)] string pluginId,
        [FromBody] SetPluginEnabledRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Plugin ID must not be empty."
            });
        }

        await allowlistService.SetEnabledAsync(pluginId, request.IsEnabled, ct);
        return NoContent();
    }

    [HttpDelete("{pluginId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveOverride(
        [FromRoute, MaxLength(128)] string pluginId,
        CancellationToken ct)
    {
        await allowlistService.RemoveOverrideAsync(pluginId, ct);
        return NoContent();
    }

    private static PluginAllowlistStatusResponse BuildStatusResponse(
        string pluginId,
        IReadOnlyDictionary<string, PluginAllowlistEntry> overrides,
        IReadOnlyDictionary<string, CalendarSourcePluginDescriptor> catalogPlugins)
    {
        var hasOverride = overrides.TryGetValue(pluginId, out var entry);
        var hasMetadata = catalogPlugins.TryGetValue(pluginId, out var plugin);

        var displayName = hasMetadata ? plugin!.DisplayName : pluginId;
        var isExternalPlugin = hasMetadata && plugin!.IsExternalPlugin;
        var isEnabled = !hasOverride || entry!.IsEnabled;
        DateTimeOffset? overrideUpdatedAtUtc = hasOverride ? entry!.UpdatedAtUtc : null;

        return new PluginAllowlistStatusResponse(
            pluginId,
            displayName,
            isExternalPlugin,
            IsEnabled: isEnabled,
            HasOverride: hasOverride,
            OverrideUpdatedAtUtc: overrideUpdatedAtUtc);
    }

    private sealed record PluginAllowlistStatusResponse(
        string PluginId,
        string DisplayName,
        bool IsExternalPlugin,
        bool IsEnabled,
        bool HasOverride,
        DateTimeOffset? OverrideUpdatedAtUtc);

    public sealed record SetPluginEnabledRequest([Required] bool IsEnabled);
}


