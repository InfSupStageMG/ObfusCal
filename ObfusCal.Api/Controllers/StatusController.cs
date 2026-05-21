using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize(Policy = AppAuthorizationPolicies.Sysadmin)]
[Route("api/status")]
public sealed class StatusController(
    IStatusService statusService,
    ISecurityAuditService securityAuditService,
    ILogger<StatusController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarOwnerStatusEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        try
        {
            await securityAuditService.WriteAsync(
                new SecurityAuditEvent(
                    SecurityAuditEventCodes.StatusRead,
                    SecurityAuditOutcomes.Success,
                    User.GetObjectId() ?? "unknown-operator",
                    "status",
                    null,
                    HttpContext.TraceIdentifier,
                    new Dictionary<string, string?>
                    {
                        ["endpoint"] = HttpContext.Request.Path.Value,
                        ["method"] = HttpContext.Request.Method
                    }),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to write status read audit event.");
        }

        var status = await statusService.GetStatusAsync(ct);
        return Ok(status);
    }
}

