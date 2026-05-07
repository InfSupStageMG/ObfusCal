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
    IGetBusySlotsUseCase getBusySlotsUseCase,
    IGetMergedFreeBusyUseCase getMergedFreeBusyUseCase,
    CalendarOwnerAccessEvaluator accessEvaluator,
    ICalendarOwnerCalendarSourceService calendarSourceService,
    ICalendarSourceInstanceService calendarSourceInstanceService,
    CalendarConsentServices consentServices,
    ICalendarOwnerIcalFeedService calendarOwnerIcalFeedService) : ControllerBase
{
    private const string ValidRedirectReq = "A valid absolute 'redirectUri' value is required.";
    private const string AuthorizationCodeIsRequired = "'authorizationCode' is required.";

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

        var sourceInstances = await calendarSourceInstanceService.ListAsync(id, ct);
        var enabledInstances = sourceInstances.Where(instance => instance.IsEnabled).ToList();
        switch (enabledInstances.Count)
        {
            case > 0 when enabledInstances.All(instance => !instance.IsReady):
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "No configured calendar sources are ready.",
                    Detail = string.Join(" ", enabledInstances.Select(instance => $"[{instance.DisplayName}] {instance.Title}"))
                });
            case 0:
            {
                var selection = await calendarSourceService.GetSelectionAsync(id, ct);
                if (selection is { IsReady: false })
                {
                    return Conflict(new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = selection.Title,
                        Detail = selection.Detail
                    });
                }

                break;
            }
        }

        var result = await getBusySlotsUseCase.ExecuteAsync(new GetBusySlotsQuery(id, from.Value, to.Value), ct);
        return Ok(result);
    }

    [HttpGet("{id}/calendar/providers")]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarSourceProviderInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListCalendarProviders(Guid id, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var providers = await calendarSourceService.ListProvidersAsync(id, ct);
        return Ok(providers);
    }

    [HttpGet("{id}/calendar/sources")]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarSourceInstanceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListCalendarSourceInstances(Guid id, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var instances = await calendarSourceInstanceService.ListAsync(id, ct);
        return Ok(instances.Select(instance => new CalendarSourceInstanceResponse(
            instance.Id,
            instance.PluginId,
            instance.DisplayName,
            instance.IsEnabled,
            instance.IsReady,
            instance.Title,
            instance.Detail,
            instance.IsExternalPlugin)).ToList());
    }

    [HttpPost("{id}/calendar/sources")]
    [ProducesResponseType(typeof(CalendarSourceInstanceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCalendarSourceInstance(Guid id, [FromBody] CreateCalendarSourceInstanceRequest request, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.PluginId))
            return BadRequest("'pluginId' is required.");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("'displayName' is required.");

        try
        {
            var created = await calendarSourceInstanceService.CreateAsync(
                id,
                new CreateCalendarSourceInstanceInput(
                    request.PluginId,
                    request.DisplayName,
                    request.ConfigurationJson,
                    request.SecretDataJson,
                    request.IsEnabled),
                ct);

            if (created is null)
                return NotFound();

            return Created(
                $"/api/calendar-owners/{id}/calendar/sources/{created.Id}",
                new CalendarSourceInstanceResponse(
                    created.Id,
                    created.PluginId,
                    created.DisplayName,
                    created.IsEnabled,
                    created.IsReady,
                    created.Title,
                    created.Detail,
                    created.IsExternalPlugin));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unknown calendar source plugin.",
                Detail = ex.Message
            });
        }
    }

    [HttpPatch("{id}/calendar/sources/{sourceInstanceId:guid}")]
    [ProducesResponseType(typeof(CalendarSourceInstanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCalendarSourceInstance(
        Guid id,
        Guid sourceInstanceId,
        [FromBody] UpdateCalendarSourceInstanceRequest request,
        CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var updated = await calendarSourceInstanceService.UpdateAsync(
            id,
            sourceInstanceId,
            new UpdateCalendarSourceInstanceInput(
                request.DisplayName,
                request.ConfigurationJson,
                request.SecretDataJson,
                request.IsEnabled),
            ct);

        if (updated is null)
            return NotFound();

        return Ok(new CalendarSourceInstanceResponse(
            updated.Id,
            updated.PluginId,
            updated.DisplayName,
            updated.IsEnabled,
            updated.IsReady,
            updated.Title,
            updated.Detail,
            updated.IsExternalPlugin));
    }

    [HttpDelete("{id}/calendar/sources/{sourceInstanceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCalendarSourceInstance(Guid id, Guid sourceInstanceId, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var deleted = await calendarSourceInstanceService.DeleteAsync(id, sourceInstanceId, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPut("{id}/calendar/provider")]
    [ProducesResponseType(typeof(CalendarSourceSelectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCalendarProvider(Guid id, [FromBody] SetCalendarProviderRequest request, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.ProviderId))
            return BadRequest("'providerId' is required.");

        try
        {
            var selection = await calendarSourceService.SetSelectionAsync(id, request.ProviderId, ct);
            if (selection is null)
                return NotFound();

            return Ok(new CalendarSourceSelectionResponse(
                selection.Id,
                selection.DisplayName,
                selection.IsReady,
                selection.Title,
                selection.Detail,
                selection.IsExternalPlugin));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unknown calendar provider.",
                Detail = ex.Message
            });
        }
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

        var status = await consentServices.Graph.GetStatusAsync(id, ct);
        if (status is null)
            return NotFound();

        return Ok(new CalendarConsentStatusResponse(
            status.HasGraphConsent,
            status.GrantedAtUtc,
            status.TokenLastRefreshedAtUtc,
            status.TokenExpiresAtUtc));
    }

    [HttpGet("{id}/calendar/graph-sources/{sourceInstanceId:guid}/status")]
    [ProducesResponseType(typeof(CalendarConsentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendarConsentStatusForSource(Guid id, Guid sourceInstanceId, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var status = await consentServices.Graph.GetStatusAsync(id, sourceInstanceId, ct);
        if (status is null)
            return NotFound();

        return Ok(new CalendarConsentStatusResponse(
            status.HasGraphConsent,
            status.GrantedAtUtc,
            status.TokenLastRefreshedAtUtc,
            status.TokenExpiresAtUtc));
    }

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

        var status = await consentServices.Google.GetStatusAsync(id, ct);
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

        var status = await consentServices.Google.GetStatusAsync(id, sourceInstanceId, ct);
        if (status is null)
            return NotFound();

        return Ok(new GoogleCalendarConsentStatusResponse(
            status.HasGoogleConsent,
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
            return BadRequest(ValidRedirectReq);

        try
        {
            var authorizationUrl = consentServices.Graph.BuildAuthorizationUrl(redirectUri);
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

    [HttpGet("{id}/calendar/graph-sources/{sourceInstanceId:guid}/consent-url")]
    [ProducesResponseType(typeof(CalendarConsentUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendarConsentUrlForSource(
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
            var authorizationUrl = await consentServices.Graph.BuildAuthorizationUrlAsync(id, sourceInstanceId, redirectUri, ct);
            return Ok(new CalendarConsentUrlResponse(authorizationUrl));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid redirect URI or Graph source instance.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("{id}/calendar/google/consent-url")]
    [ProducesResponseType(typeof(CalendarConsentUrlResponse), StatusCodes.Status200OK)]
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
            var authorizationUrl = await consentServices.Google.BuildAuthorizationUrlAsync(id, redirectUri, ct);
            return Ok(new CalendarConsentUrlResponse(authorizationUrl));
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
    [ProducesResponseType(typeof(CalendarConsentUrlResponse), StatusCodes.Status200OK)]
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
            var authorizationUrl = await consentServices.Google.BuildAuthorizationUrlAsync(id, sourceInstanceId, redirectUri, ct);
            return Ok(new CalendarConsentUrlResponse(authorizationUrl));
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
            return BadRequest(AuthorizationCodeIsRequired);

        if (string.IsNullOrWhiteSpace(request.RedirectUri) || !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
            return BadRequest(ValidRedirectReq);

        try
        {
            await consentServices.Graph.CompleteConsentAsync(id, request.AuthorizationCode, request.RedirectUri, ct);
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

    [HttpPost("{id}/calendar/graph-sources/{sourceInstanceId:guid}/consent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteCalendarConsentForSource(
        Guid id,
        Guid sourceInstanceId,
        [FromBody] CompleteCalendarConsentRequest request,
        CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.AuthorizationCode))
            return BadRequest(AuthorizationCodeIsRequired);

        if (string.IsNullOrWhiteSpace(request.RedirectUri) || !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out _))
            return BadRequest(ValidRedirectReq);

        try
        {
            await consentServices.Graph.CompleteConsentAsync(id, sourceInstanceId, request.AuthorizationCode, request.RedirectUri, ct);
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
            await consentServices.Google.CompleteConsentAsync(id, request.AuthorizationCode, request.RedirectUri, request.State, ct);
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
            await consentServices.Google.CompleteConsentAsync(
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
    [ProducesResponseType(typeof(IReadOnlyList<CalendarOwnerIcalFeedItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListIcalFeeds(Guid id, CancellationToken ct)
    {
        var accessResult = await EnsureCalendarOwnerAccessAsync(id, ct);
        if (accessResult is not null)
            return accessResult;

        var feeds = await calendarOwnerIcalFeedService.ListFeedsAsync(id, ct);
        return Ok(feeds);
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

    private sealed record GoogleCalendarConsentStatusResponse(
        bool HasGoogleConsent,
        DateTimeOffset? ConsentGrantedAtUtc,
        DateTimeOffset? TokenLastRefreshedAtUtc,
        DateTimeOffset? TokenExpiresAtUtc);

    private sealed record CalendarConsentUrlResponse(string AuthorizationUrl);

    public sealed record AddIcalFeedRequest(string FeedUrl);

    public sealed record SetCalendarProviderRequest(string ProviderId);

    private sealed record AddIcalFeedResponse(Guid Id, string FeedUrl);

    private sealed record CalendarSourceSelectionResponse(
        string Id,
        string DisplayName,
        bool IsReady,
        string Title,
        string? Detail,
        bool IsExternalPlugin);

    private sealed record CalendarSourceInstanceResponse(
        Guid Id,
        string PluginId,
        string DisplayName,
        bool IsEnabled,
        bool IsReady,
        string Title,
        string? Detail,
        bool IsExternalPlugin);


    public sealed record CompleteCalendarConsentRequest(string AuthorizationCode, string RedirectUri);

    public sealed record CompleteGoogleCalendarConsentRequest(string AuthorizationCode, string RedirectUri, string State);

    public sealed record CreateCalendarSourceInstanceRequest(
        string PluginId,
        string DisplayName,
        string? ConfigurationJson,
        string? SecretDataJson,
        bool IsEnabled = true);

    public sealed record UpdateCalendarSourceInstanceRequest(
        string? DisplayName,
        string? ConfigurationJson,
        string? SecretDataJson,
        bool? IsEnabled);
}
