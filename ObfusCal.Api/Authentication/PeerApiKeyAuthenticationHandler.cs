using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Authentication;

public sealed class PeerApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IPeerApiKeyAuthenticator peerApiKeyAuthenticator,
    ISyncRuntimeOptionsProvider syncRuntimeOptionsProvider,
    ISecurityAuditService securityAuditService,
    ILogger<PeerApiKeyAuthenticationHandler> auditLogger)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string PeerTimestampHeaderName = "X-Peer-Timestamp";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeaderValues))
            return AuthenticateResult.NoResult();

        var authorizationHeader = authorizationHeaderValues.ToString();
        var expectedPrefix = $"{PeerApiKeyAuthenticationDefaults.AuthorizationScheme} ";

        if (!authorizationHeader.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var providedApiKey = authorizationHeader[expectedPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            await WriteAuditAsync(SecurityAuditEventCodes.AuthFailure, SecurityAuditOutcomes.Failure, "missing_api_key");
            return AuthenticateResult.Fail("Missing API key.");
        }

        if (RequiresReplayTimestampValidation(Request.Path)
            && !IsRequestTimestampWithinTolerance(syncRuntimeOptionsProvider.Get()))
        {
            await WriteAuditAsync(SecurityAuditEventCodes.AuthFailure, SecurityAuditOutcomes.Failure, "invalid_replay_timestamp");
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var peer = await peerApiKeyAuthenticator.AuthenticateAsync(providedApiKey, Context.RequestAborted);

        if (peer is null)
        {
            await WriteAuditAsync(SecurityAuditEventCodes.AuthFailure, SecurityAuditOutcomes.Failure, "invalid_api_key");
            return AuthenticateResult.Fail("Invalid API key.");
        }

        await WriteAuditAsync(SecurityAuditEventCodes.AuthSuccess, SecurityAuditOutcomes.Success, "authenticated", peer.PeerInstanceId);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, peer.PeerConnectionId.ToString()),
            new Claim(PeerApiKeyClaimTypes.PeerInstanceId, peer.PeerInstanceId)
        };

        foreach (var scope in peer.Scopes)
            claims.Add(new Claim(PeerApiKeyClaimTypes.Scope, scope));

        var identity = new ClaimsIdentity(claims, PeerApiKeyAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, PeerApiKeyAuthenticationDefaults.SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    private bool IsRequestTimestampWithinTolerance(SyncOptions options)
    {
        if (!Request.Headers.TryGetValue(PeerTimestampHeaderName, out var headerValues))
            return false;

        if (!long.TryParse(headerValues.ToString(), out var unixSeconds))
            return false;

        DateTimeOffset parsedTimestamp;

        try
        {
            parsedTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        var toleranceSeconds = Math.Max(1, options.PeerRequestTimestampToleranceSeconds);
        var tolerance = TimeSpan.FromSeconds(toleranceSeconds);
        var now = DateTimeOffset.UtcNow;

        return parsedTimestamp >= now - tolerance && parsedTimestamp <= now + tolerance;
    }

    private static bool RequiresReplayTimestampValidation(PathString requestPath)
        => requestPath.StartsWithSegments("/api/shadow-slots", StringComparison.OrdinalIgnoreCase)
           || requestPath.StartsWithSegments("/api/sync/busy-slots", StringComparison.OrdinalIgnoreCase);

    private async Task WriteAuditAsync(string eventCode, string outcome, string reason, string? actorIdentity = null)
    {
        try
        {
            await securityAuditService.WriteAsync(
                new SecurityAuditEvent(
                    eventCode,
                    outcome,
                    actorIdentity ?? ResolveActorIdentity(),
                    Request.Path.Value ?? "<unknown>",
                    null,
                    Context.TraceIdentifier,
                    new Dictionary<string, string?>
                    {
                        ["reason"] = reason,
                        ["method"] = Request.Method
                    }),
                Context.RequestAborted);
        }
        catch (Exception ex)
        {
            auditLogger.LogWarning(ex,
                "Failed to write security audit event {EventCode} for peer authentication path {RequestPath}.",
                eventCode,
                Request.Path);
        }
    }

    private string ResolveActorIdentity()
    {
        if (Request.Headers.TryGetValue("X-Peer-Id", out var peerHeader) && !string.IsNullOrWhiteSpace(peerHeader.ToString()))
            return peerHeader.ToString();

        return "unknown-peer";
    }
}

