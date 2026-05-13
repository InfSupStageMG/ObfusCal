using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize(Policy = AppAuthorizationPolicies.Sysadmin)]
[Route("api/sync")]
public sealed class PeerSyncController(IPeerConnectionService peerConnectionService) : ControllerBase
{
    [HttpGet("peers")]
    [ProducesResponseType(typeof(IReadOnlyList<PeerSyncStatus>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPeerSyncStatus(CancellationToken ct)
    {
        var peers = await peerConnectionService.ListSyncStatusAsync(ct);
        return Ok(peers);
    }
}

