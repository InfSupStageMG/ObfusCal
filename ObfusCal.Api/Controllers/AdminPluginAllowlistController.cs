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

        var response = allPluginIds.Select(pluginId =>
        {
            var hasOverride = overrides.TryGetValue(pluginId, out var entry);
            var hasMetadata = catalogPlugins.TryGetValue(pluginId, out var plugin);
            return new PluginAllowlistStatusResponse(
                pluginId,
                hasMetadata ? plugin!.DisplayName : pluginId,
                hasMetadata && plugin!.IsExternalPlugin,
                IsEnabled: !hasOverride || entry!.IsEnabled,
                HasOverride: hasOverride,
                OverrideUpdatedAtUtc: hasOverride ? entry!.UpdatedAtUtc : null);
        }).ToList();

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

    private sealed record PluginAllowlistStatusResponse(
        string PluginId,
        string DisplayName,
        bool IsExternalPlugin,
        bool IsEnabled,
        bool HasOverride,
        DateTimeOffset? OverrideUpdatedAtUtc);

    public sealed record SetPluginEnabledRequest([Required] bool IsEnabled);
}


