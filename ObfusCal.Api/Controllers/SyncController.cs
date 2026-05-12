using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using System.ComponentModel.DataAnnotations;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize(Policy = AppAuthorizationPolicies.Sysadmin)]
[Route("api/sync")]
public sealed class SyncController(
    IServiceScopeFactory scopeFactory,
    ILogger<SyncController> logger) : ControllerBase
{

    [HttpPost("trigger")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult TriggerSync([FromBody] TriggerSyncRequest? request)
    {
        var callerIdentity = User.GetObjectId() ?? "unknown";

        if (request?.CalendarOwnerId is not null)
        {
            var targetOwnerId = request.CalendarOwnerId.Value;
            logger.LogInformation(
                "Manual sync triggered by {CallerIdentity} for calendar owner {CalendarOwnerId}.",
                callerIdentity,
                targetOwnerId);

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ICalendarOwnerAvailabilitySyncService>();
                try
                {
                    await syncService.RunSyncForOwnerAsync(targetOwnerId);
                }
                catch (Exception ex)
                {
                    var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<SyncController>>();
                    scopedLogger.LogWarning(ex,
                        "Background sync triggered by {CallerIdentity} failed for calendar owner {CalendarOwnerId}.",
                        callerIdentity, targetOwnerId);
                }
            });
        }
        else
        {
            logger.LogInformation(
                "Manual sync triggered by {CallerIdentity} for all calendar owners.",
                callerIdentity);

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ICalendarOwnerAvailabilitySyncService>();
                try
                {
                    await syncService.RunSyncCycleAsync();
                }
                catch (Exception ex)
                {
                    var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<SyncController>>();
                    scopedLogger.LogWarning(ex,
                        "Background sync triggered by {CallerIdentity} failed for all calendar owners.",
                        callerIdentity);
                }
            });
        }

        return Accepted();
    }

    public sealed record TriggerSyncRequest(
        [param: Range(typeof(Guid), "00000000-0000-0000-0000-000000000001", "ffffffff-ffff-ffff-ffff-ffffffffffff")]
        Guid? CalendarOwnerId = null);
}
