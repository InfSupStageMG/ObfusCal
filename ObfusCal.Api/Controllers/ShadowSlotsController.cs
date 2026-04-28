using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ObfusCal.Api.Authentication;
using ObfusCal.Application.UseCases.GetBusySlots;
using ObfusCal.Application.UseCases.PushShadowSlots;
using ObfusCal.Infrastructure.Persistence;
using Serilog;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Route("api/shadow-slots")]
public sealed class ShadowSlotsController(ISender sender, AppDbContext dbContext) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost]
    [Authorize(AuthenticationSchemes = PeerApiKeyAuthenticationDefaults.SchemeName)]
    [ProducesResponseType(typeof(void), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PushShadowSlots(
        [FromBody] JsonElement payload,
        CancellationToken ct)
    {
        var peerId = User.FindFirst(PeerApiKeyClaimTypes.PeerInstanceId)?.Value;
        if (string.IsNullOrWhiteSpace(peerId))
        {
            Log.Warning("Rejected shadow-slot push because peer authentication context is missing");
            return Unauthorized();
        }

        if (!TryParseSlots(payload, out var slots))
            return BadRequest("Request body must be a slot array or an object containing a 'slots' array.");

        await sender.Send(new PushShadowSlotsCommand(peerId, slots), ct);

        return Created($"/api/shadow-slots/{peerId}", null);
    }

    [HttpGet("/api/sync/busy-slots/{calendarOwnerRef:guid}")]
    [Authorize(AuthenticationSchemes = PeerApiKeyAuthenticationDefaults.SchemeName)]
    [ProducesResponseType(typeof(IReadOnlyList<BusySlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PullBusySlotsForPeer(
        Guid calendarOwnerRef,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var peerId = User.FindFirst(PeerApiKeyClaimTypes.PeerInstanceId)?.Value;
        if (string.IsNullOrWhiteSpace(peerId))
            return Unauthorized();

        if (from is null || to is null)
            return BadRequest("Query parameters 'from' and 'to' are required.");

        var calendarOwnerId = await dbContext.CalendarOwnerPeerMappings
            .Where(mapping => mapping.CalendarOwnerRef == calendarOwnerRef)
            .Where(mapping => mapping.PeerConnection.InstanceId == peerId)
            .Select(mapping => mapping.CalendarOwnerId)
            .SingleOrDefaultAsync(ct);

        if (calendarOwnerId == Guid.Empty)
            return Forbid();

        var slots = await sender.Send(new GetBusySlotsQuery(calendarOwnerId, from.Value, to.Value), ct);
        return Ok(slots);
    }

    private static bool TryParseSlots(JsonElement payload, out IReadOnlyList<ShadowSlotInput> slots)
    {
        slots = [];

        if (payload.ValueKind == JsonValueKind.Array)
        {
            var parsedSlots = payload.Deserialize<ShadowSlotInput[]>(JsonOptions);
            if (parsedSlots is null)
                return false;

            slots = parsedSlots;
            return true;
        }

        if (payload.ValueKind != JsonValueKind.Object)
            return false;

        var envelope = payload.Deserialize<PushShadowSlotsRequest>(JsonOptions);
        if (envelope?.Slots is null)
            return false;

        slots = envelope.Slots;
        return true;
    }

    private sealed record PushShadowSlotsRequest(Guid CalendarOwnerRef, IReadOnlyList<ShadowSlotInput> Slots);
}
