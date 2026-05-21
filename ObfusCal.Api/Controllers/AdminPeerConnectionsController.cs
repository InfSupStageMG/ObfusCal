using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.Identity.Web;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

[ApiController]
[Authorize(Policy = AppAuthorizationPolicies.Sysadmin)]
[Route("api/admin/peer-connections")]
public sealed class AdminPeerConnectionsController(
    IPeerConnectionService peerConnectionService,
    ISecurityAuditService securityAuditService,
    ILogger<AdminPeerConnectionsController> logger) : ControllerBase
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
            peer.MappingCount,
            peer.PinnedCertificateThumbprint,
            peer.ClientCertificateThumbprint)));
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
        var result = await peerConnectionService.ApproveAsync(
            id,
            request.PeerBaseUrl,
            request.Scopes,
            request.PinnedCertificateThumbprint,
            request.ClientCertificateThumbprint,
            ct);
        IActionResult actionResult = result.Outcome switch
        {
            ApprovePeerConnectionOutcome.Approved => Ok(new ApprovePeerConnectionResponse(result.PlaintextApiKey!)),
            ApprovePeerConnectionOutcome.NotFound => NotFound(),
            ApprovePeerConnectionOutcome.AlreadyActive => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Peer connection is already active.",
                Detail = "An active peer connection cannot be approved again."
            }),
            ApprovePeerConnectionOutcome.InvalidBaseUrl => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid or unsafe peer base URL.",
                Detail = "Only public https URLs are allowed for peer base addresses."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };

        if (result.Outcome == ApprovePeerConnectionOutcome.Approved)
            await WriteAuditAsync(SecurityAuditEventCodes.ConfigChange, SecurityAuditOutcomes.Success, id, "peer_connection_approved");

        return actionResult;
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
        IActionResult actionResult = result.Outcome switch
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

        if (result.Outcome == RotatePeerApiKeyOutcome.Rotated)
            await WriteAuditAsync(SecurityAuditEventCodes.KeyRotation, SecurityAuditOutcomes.Success, id, "peer_api_key_rotated");

        return actionResult;
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
        IActionResult actionResult = result.Outcome switch
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

        if (result.Outcome == RevokePeerConnectionOutcome.Revoked)
            await WriteAuditAsync(SecurityAuditEventCodes.KeyRevocation, SecurityAuditOutcomes.Success, id, "peer_revoked");

        return actionResult;
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
        int MappingCount,
        string? PinnedCertificateThumbprint,
        string? ClientCertificateThumbprint);

    public sealed record ApprovePeerConnectionRequest(
        [param: Required, MaxLength(2048)] string PeerBaseUrl,
        IReadOnlyList<string>? Scopes = null,
        [param: MaxLength(128)] string? PinnedCertificateThumbprint = null,
        [param: MaxLength(128)] string? ClientCertificateThumbprint = null);

    private sealed record ApprovePeerConnectionResponse(string ApiKey);
    private sealed record RotatePeerConnectionApiKeyResponse(string ApiKey);

    private async Task WriteAuditAsync(string eventCode, string outcome, Guid peerConnectionId, string reason)
    {
        try
        {
            var actorIdentity = User.GetObjectId() ?? "unknown-operator";
            await securityAuditService.WriteAsync(
                new SecurityAuditEvent(
                    eventCode,
                    outcome,
                    actorIdentity,
                    "admin-peer-connections",
                    peerConnectionId.ToString(),
                    HttpContext.TraceIdentifier,
                    new Dictionary<string, string?>
                    {
                        ["reason"] = reason,
                        ["method"] = HttpContext.Request.Method,
                        ["endpoint"] = HttpContext.Request.Path.Value
                    }),
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to write security audit event {EventCode} for peer connection {PeerConnectionId}.",
                eventCode,
                peerConnectionId);
        }
    }
}

