using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ObfusCal.Tests.Helpers;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string DefaultObjectId = "00000000-0000-0000-0000-000000000025";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var headerValue))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!string.Equals(headerValue.Scheme, SchemeName, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(headerValue.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid test authorization header."));
        }

        var objectId = headerValue.Parameter;

        var claims = new[]
        {
            new Claim("oid", objectId),
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", objectId),
            new Claim(ClaimTypes.NameIdentifier, objectId),
            new Claim(ClaimTypes.Name, "Integration Test Calendar Owner")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

