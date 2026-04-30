using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Application.UseCases.GetBusySlots;
using ObfusCal.Application.UseCases.GetMergedFreeBusy;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/calendar-owners")]
public sealed class CalendarOwnersController(
    IGetBusySlotsUseCase getBusySlotsUseCase,
    IGetMergedFreeBusyUseCase getMergedFreeBusyUseCase,
    CalendarOwnerAccessEvaluator accessEvaluator,
    ICalendarOwnerGraphConsentService graphConsentService,
    ICalendarOwnerIcalFeedService calendarOwnerIcalFeedService,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService) : ControllerBase
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

        var result = await getBusySlotsUseCase.ExecuteAsync(new GetBusySlotsQuery(id, from.Value, to.Value), ct);
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

        if (string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest("A valid absolute 'redirectUri' query parameter is required.");

        try
        {
            var authorizationUrl = graphConsentService.BuildAuthorizationUrl(redirectUri);
            return Ok(new CalendarConsentUrlResponse(authorizationUrl));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid redirect URI.",
                Detail = ex.Message
            });
        }
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

        var result = await getMergedFreeBusyUseCase.ExecuteAsync(new GetMergedFreeBusyQuery(id, from.Value, to.Value), ct);
        return Ok(result);
    }

    [HttpGet("{id}/obfuscation-profiles")]
    [ProducesResponseType(typeof(IReadOnlyList<ObfuscationProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListObfuscationProfiles(Guid id, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var profiles = await obfuscationProfileService.GetProfilesAsync(id, ct);
        return Ok(profiles.Select(ToResponse).ToList());
    }

    [HttpPut("{id}/obfuscation-profiles/{context}")]
    [ProducesResponseType(typeof(ObfuscationProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetObfuscationProfile(
        Guid id,
        string context,
        [FromBody] SetObfuscationProfileRequest request,
        CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (!Enum.TryParse<ObfuscationAuditContext>(context, true, out var parsedContext))
            return BadRequest("Context must be one of: Internal, Client.");

        if (request.RoundingIntervalMinutes <= 0)
            return BadRequest("'roundingIntervalMinutes' must be greater than zero.");

        var updated = await obfuscationProfileService.SetProfileAsync(
            id,
            new ObfuscationProfileSettings(
                parsedContext,
                request.RemoveTitle,
                request.RemoveDescription,
                request.RemoveLocation,
                request.RemoveAttendees,
                request.RoundTimes,
                request.RoundingIntervalMinutes,
                request.MergeBlocks),
            ct);

        return Ok(ToResponse(updated));
    }

    private static ObfuscationProfileResponse ToResponse(ObfuscationProfileSettings profile) =>
        new(
            profile.Context.ToString(),
            profile.RemoveTitle,
            profile.RemoveDescription,
            profile.RemoveLocation,
            profile.RemoveAttendees,
            profile.RoundTimes,
            profile.RoundingIntervalMinutes,
            profile.MergeBlocks);

    [HttpPost("{id}/ical-feeds")]
    [ProducesResponseType(typeof(AddIcalFeedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddIcalFeed(Guid id, [FromBody] AddIcalFeedRequest request, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.FeedUrl))
            return BadRequest("'feedUrl' is required.");

        if (!Uri.TryCreate(request.FeedUrl, UriKind.Absolute, out var feedUri))
            return BadRequest("'feedUrl' must be a valid absolute URI.");

        if (!string.Equals(feedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(feedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("'feedUrl' must use the http or https scheme.");
        }

        var result = await calendarOwnerIcalFeedService.AddFeedAsync(id, feedUri.AbsoluteUri, ct);
        return result.Outcome switch
        {
            AddCalendarOwnerIcalFeedOutcome.Added => Created(
                $"/api/calendar-owners/{id}/ical-feeds/{result.FeedId}",
                new AddIcalFeedResponse(result.FeedId!.Value, result.FeedUrl!)),
            AddCalendarOwnerIcalFeedOutcome.Duplicate => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "iCal feed already exists for this calendar owner.",
                Detail = "The provided feed URL is already configured for this calendar owner."
            }),
            AddCalendarOwnerIcalFeedOutcome.CalendarOwnerNotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{id}/ical-feeds")]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarOwnerIcalFeedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListIcalFeeds(Guid id, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var feeds = await calendarOwnerIcalFeedService.ListFeedsAsync(id, ct);
        return Ok(feeds.Select(feed => new CalendarOwnerIcalFeedResponse(feed.Id, feed.FeedUrl)).ToList());
    }

    [HttpDelete("{id}/ical-feeds/{feedId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteIcalFeed(Guid id, Guid feedId, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var result = await calendarOwnerIcalFeedService.DeleteFeedAsync(id, feedId, ct);
        return result.Outcome switch
        {
            DeleteCalendarOwnerIcalFeedOutcome.Deleted => NoContent(),
            DeleteCalendarOwnerIcalFeedOutcome.FeedNotFound => NotFound(),
            DeleteCalendarOwnerIcalFeedOutcome.CalendarOwnerNotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
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

    public sealed record AddIcalFeedRequest(string FeedUrl);

    private sealed record AddIcalFeedResponse(Guid Id, string FeedUrl);

    private sealed record CalendarOwnerIcalFeedResponse(Guid Id, string FeedUrl);

    public sealed record SetObfuscationProfileRequest(
        bool RemoveTitle,
        bool RemoveDescription,
        bool RemoveLocation,
        bool RemoveAttendees,
        bool RoundTimes,
        int RoundingIntervalMinutes,
        bool MergeBlocks);

    private sealed record ObfuscationProfileResponse(
        string Context,
        bool RemoveTitle,
        bool RemoveDescription,
        bool RemoveLocation,
        bool RemoveAttendees,
        bool RoundTimes,
        int RoundingIntervalMinutes,
        bool MergeBlocks);

    public sealed record CompleteCalendarConsentRequest(string AuthorizationCode, string RedirectUri);
}
