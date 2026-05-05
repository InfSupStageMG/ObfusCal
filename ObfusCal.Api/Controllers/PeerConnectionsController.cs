using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/peer-connections")]
public sealed class PeerConnectionsController(
    ICalendarOwnerScopeResolver calendarOwnerScopeResolver,
    IPeerConnectionService peerConnectionService) : ControllerBase
{
    [HttpPost("request")]
    [ProducesResponseType(typeof(CreatePeerConnectionRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestPeerConnection([FromBody] RequestPeerConnectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClientOrganisationName))
            return BadRequest("'clientOrganisationName' is required.");

        var calendarOwnerScope = await ResolveCalendarOwnerScopeAsync(ct);
        if (calendarOwnerScope is null)
            return Unauthorized();

        var result = await peerConnectionService.CreateRequestAsync(
            calendarOwnerScope.CalendarOwnerId,
            request.ClientOrganisationName,
            ct);

        return result.Outcome switch
        {
            CreatePeerConnectionRequestOutcome.Created => Created(
                $"/api/peer-connections/{result.PeerConnectionId}",
                new CreatePeerConnectionRequestResponse(result.PeerConnectionId!.Value)),
            CreatePeerConnectionRequestOutcome.Duplicate => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Peer connection request already exists.",
                Detail = "A request for this client organisation already exists for the current consultant."
            }),
            CreatePeerConnectionRequestOutcome.CalendarOwnerNotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PeerConnectionRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListPeerConnections(CancellationToken ct)
    {
        var calendarOwnerScope = await ResolveCalendarOwnerScopeAsync(ct);
        if (calendarOwnerScope is null)
            return Unauthorized();

        var connections = await peerConnectionService.ListForCalendarOwnerAsync(calendarOwnerScope.CalendarOwnerId, ct);
        return Ok(connections.Select(connection => new PeerConnectionRequestResponse(
            connection.Id,
            connection.ClientOrganisationName,
            connection.Status.ToString())));
    }

    private async Task<CalendarOwnerScope?> ResolveCalendarOwnerScopeAsync(CancellationToken ct)
    {
        var objectId = User.GetObjectId();
        if (string.IsNullOrWhiteSpace(objectId))
            return null;

        return await calendarOwnerScopeResolver.FindByEntraObjectIdAsync(objectId, ct);
    }

    public sealed record RequestPeerConnectionRequest(string ClientOrganisationName);

    private sealed record CreatePeerConnectionRequestResponse(Guid Id);

    private sealed record PeerConnectionRequestResponse(Guid Id, string ClientOrganisationName, string Status);
}

