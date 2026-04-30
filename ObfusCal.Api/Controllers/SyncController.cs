using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sync")]
public sealed class SyncController(IPeerConnectionService peerConnectionService) : ControllerBase
{
    [HttpGet("peers")]
    [ProducesResponseType(typeof(IReadOnlyList<PeerSyncStatus>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPeerSyncStatus(CancellationToken ct)
    {
        var peers = await peerConnectionService.ListSyncStatusAsync(ct);
        return Ok(peers);
    }
}

