using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.UseCases.GetBusySlots;
using ObfusCal.Application.UseCases.GetMergedFreeBusy;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/calendar-owners")]
public sealed class CalendarOwnersController(
    ISender sender,
    CalendarOwnerAccessEvaluator accessEvaluator) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentCalendarOwnerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentCalendarOwner()
    {
        var objectId = User.GetObjectId();
        if (string.IsNullOrWhiteSpace(objectId))
            return Unauthorized();

        return Ok(new CurrentCalendarOwnerResponse(objectId));
    }

    [HttpGet("{id}/busy-slots")]
    public async Task<IActionResult> GetBusySlots(
        Guid id,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (from is null || to is null)
            return BadRequest("Query parameters 'from' and 'to' are required.");

        var result = await sender.Send(new GetBusySlotsQuery(id, from.Value, to.Value), ct);
        return Ok(result);
    }

    [HttpGet("{id}/merged-freebusy")]
    public async Task<IActionResult> GetMergedFreeBusy(
        Guid id,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (from is null || to is null)
            return BadRequest("Query parameters 'from' and 'to' are required.");

        var result = await sender.Send(new GetMergedFreeBusyQuery(id, from.Value, to.Value), ct);
        return Ok(result);
    }

    private async Task<IActionResult?> EnsureCalendarOwnerAccessAsync(Guid requestedCalendarOwnerId, CancellationToken ct)
    {
        var accessResult = await accessEvaluator.EvaluateAsync(User, requestedCalendarOwnerId, ct);
        return accessResult.Status switch
        {
            CalendarOwnerAccessStatus.Allowed => null,
            CalendarOwnerAccessStatus.Forbidden => Forbid(),
            CalendarOwnerAccessStatus.NotFound => NotFound(),
            _ => Unauthorized()
        };
    }

    private sealed record CurrentCalendarOwnerResponse(string ObjectId);
}
