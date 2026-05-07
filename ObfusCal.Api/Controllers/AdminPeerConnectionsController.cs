using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize(Roles = "Sysadmin")]
[Route("api/admin/peer-connections")]
public sealed class AdminPeerConnectionsController(IPeerConnectionService peerConnectionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminPeerConnectionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAll(CancellationToken ct)
    {
        var peers = await peerConnectionService.ListForAdminAsync(ct);
        return Ok(peers.Select(peer => new AdminPeerConnectionResponse(
            peer.Id,
            peer.InstanceId,
            peer.BaseAddress,
            peer.Status.ToString(),
            peer.ClientOrganisationName,
            peer.RequestedByCalendarOwnerId,
            peer.RequestedByCalendarOwnerName,
            peer.MappingCount)));
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApprovePeerConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovePeerConnectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PeerBaseUrl))
            return BadRequest("'peerBaseUrl' is required.");

        if (!Uri.TryCreate(request.PeerBaseUrl, UriKind.Absolute, out var parsedUri))
            return BadRequest("'peerBaseUrl' must be a valid absolute URI.");

        if (!string.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("'peerBaseUrl' must use the http or https scheme.");
        }

        var result = await peerConnectionService.ApproveAsync(id, parsedUri.AbsoluteUri, request.Scopes, ct);
        return result.Outcome switch
        {
            ApprovePeerConnectionOutcome.Approved => Ok(new ApprovePeerConnectionResponse(result.PlaintextApiKey!)),
            ApprovePeerConnectionOutcome.NotFound => NotFound(),
            ApprovePeerConnectionOutcome.AlreadyActive => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Peer connection is already active.",
                Detail = "An active peer connection cannot be approved again."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:guid}/rotate-key")]
    [ProducesResponseType(typeof(RotatePeerConnectionApiKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RotateApiKey(Guid id, CancellationToken ct)
    {
        var result = await peerConnectionService.RotateApiKeyAsync(id, ct);
        return result.Outcome switch
        {
            RotatePeerApiKeyOutcome.Rotated => Ok(new RotatePeerConnectionApiKeyResponse(result.PlaintextApiKey!)),
            RotatePeerApiKeyOutcome.NotFound => NotFound(),
            RotatePeerApiKeyOutcome.NotActive => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Peer connection must be active to rotate keys."
            }),
            RotatePeerApiKeyOutcome.Revoked => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Peer connection has already been revoked."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var result = await peerConnectionService.RevokeAsync(id, ct);
        return result.Outcome switch
        {
            RevokePeerConnectionOutcome.Revoked => NoContent(),
            RevokePeerConnectionOutcome.NotFound => NotFound(),
            RevokePeerConnectionOutcome.AlreadyRevoked => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Peer connection has already been revoked."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var suspended = await peerConnectionService.SuspendAsync(id, ct);
        return suspended ? NoContent() : NotFound();
    }

    private sealed record AdminPeerConnectionResponse(
        Guid Id,
        string InstanceId,
        string BaseAddress,
        string Status,
        string? ClientOrganisationName,
        Guid? RequestedByCalendarOwnerId,
        string? RequestedByCalendarOwnerName,
        int MappingCount);

    public sealed record ApprovePeerConnectionRequest(string PeerBaseUrl, IReadOnlyList<string>? Scopes = null);

    private sealed record ApprovePeerConnectionResponse(string ApiKey);
    private sealed record RotatePeerConnectionApiKeyResponse(string ApiKey);
}

