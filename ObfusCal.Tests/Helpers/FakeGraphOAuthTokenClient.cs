using ObfusCal.Application.Interfaces;

namespace ObfusCal.Tests.Helpers;

public sealed class FakeGraphOAuthTokenClient : IGraphOAuthTokenClient
{
    public const string ValidAuthorizationCode = "valid-consent-code";
    public const string AccessToken = "graph-access-token";
    public const string RefreshToken = "graph-refresh-token";

    public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
        string authorizationCode,
        string redirectUri,
        string? scope = null,
        CancellationToken ct = default)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
            throw new InvalidOperationException("Redirect URI must be absolute.");

        if (!string.Equals(authorizationCode, ValidAuthorizationCode, StringComparison.Ordinal))
            throw new InvalidOperationException("The authorization code is invalid or expired.");

        return Task.FromResult(new GraphOAuthTokenResponse(
            AccessToken,
            RefreshToken,
            scope ?? "https://graph.microsoft.com/Calendars.ReadWrite offline_access",
            DateTimeOffset.UtcNow.AddHours(1)));
    }

    public Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, string? scope = null, CancellationToken ct = default)
    {
        if (!string.Equals(refreshToken, RefreshToken, StringComparison.Ordinal))
            throw new InvalidOperationException("The refresh token is invalid or expired.");

        return Task.FromResult(new GraphOAuthTokenResponse(
            AccessToken,
            RefreshToken,
            scope ?? "https://graph.microsoft.com/Calendars.ReadWrite offline_access",
            DateTimeOffset.UtcNow.AddHours(1)));
    }
}

