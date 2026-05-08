using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/calendar-owners")]
public sealed class CalendarOwnerGoogleConsentController(
    CalendarOwnerAccessEvaluator accessEvaluator,
    ICalendarOwnerGoogleConsentService googleConsentService) : ControllerBase
{
    private const string ValidRedirectReq = "A valid absolute 'redirectUri' value is required.";
    private const string AuthorizationCodeIsRequired = "'authorizationCode' is required.";

    [HttpGet("{id}/calendar/google/status")]
    [ProducesResponseType(typeof(GoogleCalendarConsentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGoogleCalendarConsentStatus(Guid id, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var status = await googleConsentService.GetStatusAsync(id, ct);
        if (status is null)
            return NotFound();

        return Ok(new GoogleCalendarConsentStatusResponse(
            status.HasGoogleConsent,
            status.GrantedAtUtc,
            status.TokenLastRefreshedAtUtc,
            status.TokenExpiresAtUtc));
    }

    [HttpGet("{id}/calendar/google-sources/{sourceInstanceId:guid}/status")]
    [ProducesResponseType(typeof(GoogleCalendarConsentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGoogleCalendarConsentStatusForSource(Guid id, Guid sourceInstanceId, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var status = await googleConsentService.GetStatusAsync(id, sourceInstanceId, ct);
        if (status is null)
            return NotFound();

        return Ok(new GoogleCalendarConsentStatusResponse(
            status.HasGoogleConsent,
            status.GrantedAtUtc,
            status.TokenLastRefreshedAtUtc,
            status.TokenExpiresAtUtc));
    }

    [HttpGet("{id}/calendar/google/consent-url")]
    [ProducesResponseType(typeof(GoogleCalendarConsentUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGoogleCalendarConsentUrl(Guid id, [FromQuery] string? redirectUri, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(ValidRedirectReq);

        try
        {
            var authorizationUrl = await googleConsentService.BuildAuthorizationUrlAsync(id, redirectUri, ct);
            return Ok(new GoogleCalendarConsentUrlResponse(authorizationUrl));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid redirect URI or Google configuration.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{id}/calendar/google-sources/{sourceInstanceId:guid}/consent-url")]
    [ProducesResponseType(typeof(GoogleCalendarConsentUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGoogleCalendarConsentUrlForSource(
        Guid id,
        Guid sourceInstanceId,
        [FromQuery] string? redirectUri,
        CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest(ValidRedirectReq);

        try
        {
            var authorizationUrl = await googleConsentService.BuildAuthorizationUrlAsync(id, sourceInstanceId, redirectUri, ct);
            return Ok(new GoogleCalendarConsentUrlResponse(authorizationUrl));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid redirect URI or Google source instance.",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("{id}/calendar/google/consent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteGoogleCalendarConsent(
        Guid id,
        [FromBody] CompleteGoogleCalendarConsentRequest request,
        CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.AuthorizationCode))
            return BadRequest(AuthorizationCodeIsRequired);

        if (string.IsNullOrWhiteSpace(request.State))
            return BadRequest("'state' is required.");

        if (string.IsNullOrWhiteSpace(request.RedirectUri) || !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
            return BadRequest(ValidRedirectReq);

        try
        {
            await googleConsentService.CompleteConsentAsync(id, request.AuthorizationCode, request.RedirectUri, request.State, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unable to complete Google consent.",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("{id}/calendar/google-sources/{sourceInstanceId:guid}/consent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteGoogleCalendarConsentForSource(
        Guid id,
        Guid sourceInstanceId,
        [FromBody] CompleteGoogleCalendarConsentRequest request,
        CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.AuthorizationCode))
            return BadRequest(AuthorizationCodeIsRequired);

        if (string.IsNullOrWhiteSpace(request.State))
            return BadRequest("'state' is required.");

        if (string.IsNullOrWhiteSpace(request.RedirectUri) || !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
            return BadRequest(ValidRedirectReq);

        try
        {
            await googleConsentService.CompleteConsentAsync(
                id,
                sourceInstanceId,
                request.AuthorizationCode,
                request.RedirectUri,
                request.State,
                ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unable to complete Google consent.",
                Detail = ex.Message
            });
        }
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

    private sealed record GoogleCalendarConsentStatusResponse(
        bool HasGoogleConsent,
        DateTimeOffset? ConsentGrantedAtUtc,
        DateTimeOffset? TokenLastRefreshedAtUtc,
        DateTimeOffset? TokenExpiresAtUtc);

    private sealed record GoogleCalendarConsentUrlResponse(string AuthorizationUrl);

    public sealed record CompleteGoogleCalendarConsentRequest(string AuthorizationCode, string RedirectUri, string State);
}

