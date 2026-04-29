using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sync")]
public sealed class SyncController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("peers")]
    [ProducesResponseType(typeof(IReadOnlyList<PeerSyncStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPeerSyncStatus(CancellationToken ct)
    {
        var peers = await dbContext.PeerConnections
            .AsNoTracking()
            .OrderBy(p => p.InstanceId)
            .Select(p => new PeerSyncStatusResponse(
                p.InstanceId,
                p.BaseAddress,
                p.LastSyncedAt,
                p.LastSyncSucceeded))
            .ToListAsync(ct);

        return Ok(peers);
    }

    private sealed record PeerSyncStatusResponse(
        string InstanceId,
        string BaseAddress,
        DateTimeOffset? LastSyncedAt,
        bool? LastSyncSucceeded);
}

