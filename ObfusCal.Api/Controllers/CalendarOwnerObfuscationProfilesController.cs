using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/calendar-owners")]
public sealed class CalendarOwnerObfuscationProfilesController(
    CalendarOwnerAccessEvaluator accessEvaluator,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService) : ControllerBase
{
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
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Context must be one of: Internal, Client."
            });

        if (request.RoundingIntervalMinutes <= 0)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "'roundingIntervalMinutes' must be greater than zero."
            });

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

    private async Task<IActionResult?> EnsureCalendarOwnerAccessAsync(Guid requestedCalendarOwnerId, CancellationToken ct)
    {
        var accessResult = await accessEvaluator.EvaluateAsync(User, requestedCalendarOwnerId, ct);
        return accessResult.Status switch
        {
            CalendarOwnerAccessStatus.Allowed => null,
            CalendarOwnerAccessStatus.Forbidden => NotFound(),
            CalendarOwnerAccessStatus.NotFound => NotFound(),
            _ => Unauthorized()
        };
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
}

