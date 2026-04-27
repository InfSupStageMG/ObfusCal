using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.UseCases.GetBusySlots;
using ObfusCal.Application.UseCases.GetMergedFreeBusy;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/calendar-owners")]
public sealed class CalendarOwnersController(
    ISender sender,
    CalendarOwnerAccessEvaluator accessEvaluator,
    ICalendarOwnerGraphConsentService graphConsentService) : ControllerBase
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

        var hasConsent = await graphConsentService.HasConsentAsync(id, ct);
        if (!hasConsent)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Microsoft Graph consent required.",
                Detail = "This calendar owner has not granted Microsoft Graph calendar consent yet. Complete consent before requesting busy slots."
            });
        }

        var result = await sender.Send(new GetBusySlotsQuery(id, from.Value, to.Value), ct);
        return Ok(result);
    }

    [HttpGet("{id}/calendar/status")]
    [ProducesResponseType(typeof(CalendarConsentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendarConsentStatus(Guid id, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var status = await graphConsentService.GetStatusAsync(id, ct);
        if (status is null)
            return NotFound();

        return Ok(new CalendarConsentStatusResponse(
            status.HasGraphConsent,
            status.GrantedAtUtc,
            status.TokenLastRefreshedAtUtc,
            status.TokenExpiresAtUtc));
    }

    [HttpGet("{id}/calendar/consent-url")]
    [ProducesResponseType(typeof(CalendarConsentUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendarConsentUrl(Guid id, [FromQuery] string? redirectUri, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(redirectUri) || !Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
            return BadRequest("A valid absolute 'redirectUri' query parameter is required.");

        var authorizationUrl = graphConsentService.BuildAuthorizationUrl(redirectUri);
        return Ok(new CalendarConsentUrlResponse(authorizationUrl));
    }

    [HttpPost("{id}/calendar/consent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteCalendarConsent(
        Guid id,
        [FromBody] CompleteCalendarConsentRequest request,
        CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.AuthorizationCode))
            return BadRequest("'authorizationCode' is required.");

        if (string.IsNullOrWhiteSpace(request.RedirectUri) || !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
            return BadRequest("A valid absolute 'redirectUri' is required.");

        try
        {
            await graphConsentService.CompleteConsentAsync(id, request.AuthorizationCode, request.RedirectUri, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unable to complete Microsoft Graph consent.",
                Detail = ex.Message
            });
        }
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

    private sealed record CalendarConsentStatusResponse(
        bool HasGraphConsent,
        DateTimeOffset? ConsentGrantedAtUtc,
        DateTimeOffset? TokenLastRefreshedAtUtc,
        DateTimeOffset? TokenExpiresAtUtc);

    private sealed record CalendarConsentUrlResponse(string AuthorizationUrl);

    public sealed record CompleteCalendarConsentRequest(string AuthorizationCode, string RedirectUri);
}
