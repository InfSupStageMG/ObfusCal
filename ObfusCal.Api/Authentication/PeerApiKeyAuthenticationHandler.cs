using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Api.Authentication;

public sealed class PeerApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext dbContext)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
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

        var hashedApiKey = PeerApiKeySecurity.ComputeSha256(providedApiKey);

        var peer = await dbContext.PeerConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(
                connection => connection.Status == PeerConnectionStatus.Active
                              && connection.ApiKeyHash == hashedApiKey,
                Context.RequestAborted);

        if (peer is null)
            return AuthenticateResult.Fail("Invalid API key.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, peer.Id.ToString()),
            new Claim(PeerApiKeyClaimTypes.PeerInstanceId, peer.InstanceId)
        };

        var identity = new ClaimsIdentity(claims, PeerApiKeyAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, PeerApiKeyAuthenticationDefaults.SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}

