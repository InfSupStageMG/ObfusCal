using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ObfusCal.Core.Configuration;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Route("api/shadow-slots")]
public sealed class ShadowSlotsController(IShadowSlotStore shadowSlotStore, IOptions<SyncOptions> syncOptions) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(void), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PushShadowSlots(
        [FromHeader(Name = "X-Peer-Id")] string? peerId,
        [FromBody] IReadOnlyList<BusySlotPushRequest> slots,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peerId))
            return BadRequest("Header 'X-Peer-Id' is required.");

        var isKnownPeer = syncOptions.Value.KnownPeerIds
            .Any(id => string.Equals(id, peerId, StringComparison.OrdinalIgnoreCase));

        if (!isKnownPeer)
            return Unauthorized();

        var storedSlots = slots
            .Select((slot, index) => new BusySlot($"{peerId}-{index}", slot.Start, slot.End))
            .ToArray();

        await shadowSlotStore.SetSlotsAsync(peerId, storedSlots, ct);
        return Created($"/api/shadow-slots/{peerId}", null);
    }

    public sealed record BusySlotPushRequest(DateTimeOffset Start, DateTimeOffset End);
}
