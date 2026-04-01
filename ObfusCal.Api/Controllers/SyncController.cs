using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ObfusCal.Core.Configuration;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;
using ObfusCal.Core.Obfuscation;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Route("api/sync")]
public class SyncController(
    ICalendarSource calendarSource,
    ObfuscationPipeline pipeline,
    IShadowSlotStore store,
    IOptions<SyncOptions> opts,
    ILogger<SyncController> logger) : ControllerBase
{
    private readonly SyncOptions _opts = opts.Value;

    // Peers call this to pull our obfuscated slots
    [HttpGet("pull")]
    public async Task<IReadOnlyList<BusySlot>> Pull(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken ct)
    {
        var events = await calendarSource.GetEventsAsync(from, to, ct);
        return pipeline.Process(events);
    }

    // Peers call this to push their obfuscated slots to us
    [HttpPost("push")]
    public async Task<IActionResult> Push(
        [FromBody] List<BusySlot> slots,
        [FromHeader(Name = "X-Peer-Id")] string? peerId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peerId))
            return BadRequest("X-Peer-Id header is required");

        if (_opts.Peers.All(p => p.Id != peerId))
            return Unauthorized($"Unknown peer: {peerId}");

        await store.SaveAsync(peerId, slots, ct);
        logger.LogInformation("Stored {Count} slots from {PeerId}", slots.Count, peerId);
        return Ok();
    }
}