using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ObfusCal.Api.Authentication;
using ObfusCal.Api.RateLimiting;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.UseCases.GetBusySlots;
using ObfusCal.Application.UseCases.PushShadowSlots;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Route("api/shadow-slots")]
public sealed class ShadowSlotsController(
    IGetBusySlotsUseCase getBusySlotsUseCase,
    IPushShadowSlotsUseCase pushShadowSlotsUseCase,
    IPeerCalendarOwnerResolver peerCalendarOwnerResolver,
    ILogger<ShadowSlotsController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost]
    [Authorize(AuthenticationSchemes = PeerApiKeyAuthenticationDefaults.SchemeName, Policy = PeerApiAuthorizationPolicies.PushShadowSlots)]
    [EnableRateLimiting(PeerRateLimiting.PushShadowSlotsPolicyName)]
    [ProducesResponseType(typeof(void), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> PushShadowSlots(
        [FromBody] JsonElement payload,
        CancellationToken ct)
    {
        var peerId = User.FindFirst(PeerApiKeyClaimTypes.PeerInstanceId)?.Value;
        if (string.IsNullOrWhiteSpace(peerId))
        {
            logger.LogWarning("Rejected shadow-slot push because peer authentication context is missing");
            return Unauthorized();
        }

        if (!TryParseSlots(payload, out var parsedPayload))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request body must be a slot array or an object containing a 'slots' array."
            });

        var calendarOwnerIds = parsedPayload.CalendarOwnerRef is { } calendarOwnerRef
            ? await peerCalendarOwnerResolver.ResolveCalendarOwnerIdsAsync(peerId, calendarOwnerRef, ct)
            : await peerCalendarOwnerResolver.ResolveAllCalendarOwnerIdsAsync(peerId, ct);

        if (calendarOwnerIds.Count == 0)
        {
            if (parsedPayload.CalendarOwnerRef is { } unmappedOwnerRef)
            {
                logger.LogWarning(
                    "Rejected shadow-slot push because peer {PeerId} is not mapped to requested calendar owner ref {CalendarOwnerRef}",
                    peerId,
                    unmappedOwnerRef);
            }

            return Forbid();
        }

        await pushShadowSlotsUseCase.ExecuteAsync(new PushShadowSlotsCommand(peerId, calendarOwnerIds, parsedPayload.Slots), ct);

        return Created($"/api/shadow-slots/{peerId}", null);
    }

    [HttpGet("/api/sync/busy-slots/{calendarOwnerRef:guid}")]
    [Authorize(AuthenticationSchemes = PeerApiKeyAuthenticationDefaults.SchemeName, Policy = PeerApiAuthorizationPolicies.PullBusySlots)]
    [EnableRateLimiting(PeerRateLimiting.PullBusySlotsPolicyName)]
    [ProducesResponseType(typeof(IReadOnlyList<BusySlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> PullBusySlotsForPeer(
        Guid calendarOwnerRef,
        [FromQuery] CalendarOwnersController.TimeWindowQuery query,
        CancellationToken ct)
    {
        var peerId = User.FindFirst(PeerApiKeyClaimTypes.PeerInstanceId)?.Value;
        if (string.IsNullOrWhiteSpace(peerId))
            return Unauthorized();

        var calendarOwnerId = await peerCalendarOwnerResolver.ResolveSingleCalendarOwnerIdAsync(peerId, calendarOwnerRef, ct);

        if (calendarOwnerId == Guid.Empty)
            return Forbid();

        var slots = await getBusySlotsUseCase.ExecuteAsync(new GetBusySlotsQuery(calendarOwnerId, query.From!.Value, query.To!.Value), ct);
        return Ok(slots);
    }

    private static bool TryParseSlots(JsonElement payload, out ParsedShadowSlotsPayload parsedPayload)
    {
        parsedPayload = new ParsedShadowSlotsPayload(null, []);

        if (payload.ValueKind == JsonValueKind.Array)
        {
            var parsedSlots = payload.Deserialize<ShadowSlotInput[]>(JsonOptions);
            if (parsedSlots is null)
                return false;

            parsedPayload = new ParsedShadowSlotsPayload(null, parsedSlots);
            return true;
        }

        if (payload.ValueKind != JsonValueKind.Object)
            return false;

        var envelope = payload.Deserialize<PushShadowSlotsRequest>(JsonOptions);
        if (envelope?.Slots is null)
            return false;

        parsedPayload = new ParsedShadowSlotsPayload(envelope.CalendarOwnerRef, envelope.Slots);
        return true;
    }

    private sealed record ParsedShadowSlotsPayload(Guid? CalendarOwnerRef, IReadOnlyList<ShadowSlotInput> Slots);

    private sealed record PushShadowSlotsRequest(
        [property: Required] Guid? CalendarOwnerRef,
        [property: Required, MinLength(1)] IReadOnlyList<ShadowSlotInput> Slots);
}
