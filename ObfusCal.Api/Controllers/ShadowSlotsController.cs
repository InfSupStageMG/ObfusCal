using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.UseCases.PushShadowSlots;
using Serilog;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Route("api/shadow-slots")]
public sealed class ShadowSlotsController(ISender sender, IOptions<SyncOptions> syncOptions) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(void), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PushShadowSlots(
        [FromHeader(Name = "X-Peer-Id")] string? peerId,
        [FromBody] IReadOnlyList<ShadowSlotInput> slots,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            Log.Warning("Rejected shadow-slot push because X-Peer-Id header is missing");
            return BadRequest("Header 'X-Peer-Id' is required.");
        }

        var isKnownPeer = syncOptions.Value.KnownPeerIds
            .Any(id => string.Equals(id, peerId, StringComparison.OrdinalIgnoreCase));

        if (!isKnownPeer)
        {
            Log.ForContext("PeerId", peerId)
                .Warning("Rejected shadow-slot push because peer is unknown");
            return Unauthorized();
        }

        await sender.Send(new PushShadowSlotsCommand(peerId, slots), ct);

        return Created($"/api/shadow-slots/{peerId}", null);
    }
}
