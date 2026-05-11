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
    ISyncRuntimeOptionsProvider syncRuntimeOptionsProvider)
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
            return AuthenticateResult.Fail("Missing API key.");

        if (RequiresReplayTimestampValidation(Request.Path)
            && !IsRequestTimestampWithinTolerance(syncRuntimeOptionsProvider.Get()))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var peer = await peerApiKeyAuthenticator.AuthenticateAsync(providedApiKey, Context.RequestAborted);

        if (peer is null)
            return AuthenticateResult.Fail("Invalid API key.");

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
}

