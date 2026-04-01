using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ObfusCal.Core.Configuration;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;
using ObfusCal.Core.Obfuscation;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Route("api/freebusy")]
public class FreeBusyController(
    ICalendarSource calendarSource,
    ObfuscationPipeline pipeline,
    IShadowSlotStore store,
    IOptions<SyncOptions> opts) : ControllerBase
{
    private readonly SyncOptions _opts = opts.Value;

    [HttpGet]
    public async Task<FreeBusyResponse> Get(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var start = from ?? DateTimeOffset.UtcNow;
        var end   = to   ?? start.AddDays(_opts.LookAheadDays);

        var rawEvents = await calendarSource.GetEventsAsync(start, end, ct);
        var ownSlots  = pipeline.Process(rawEvents);

        var peerIds   = await store.GetPeerIdsAsync(ct);
        var peerSlots = new Dictionary<string, IReadOnlyList<BusySlot>>();
        foreach (var peerId in peerIds)
            peerSlots[peerId] = await store.GetAsync(peerId, start, end, ct);

        return new FreeBusyResponse(_opts.InstanceId, start, end, ownSlots, peerSlots);
    }
}

public record FreeBusyResponse(
    string InstanceId,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<BusySlot> OwnSlots,
    Dictionary<string, IReadOnlyList<BusySlot>> PeerSlots
);