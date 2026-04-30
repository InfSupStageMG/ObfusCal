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
        CancellationToken ct = default)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
            throw new InvalidOperationException("Redirect URI must be absolute.");

        if (!string.Equals(authorizationCode, ValidAuthorizationCode, StringComparison.Ordinal))
            throw new InvalidOperationException("The authorization code is invalid or expired.");

        return Task.FromResult(new GraphOAuthTokenResponse(
            AccessToken,
            RefreshToken,
            DateTimeOffset.UtcNow.AddHours(1)));
    }

    public Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (!string.Equals(refreshToken, RefreshToken, StringComparison.Ordinal))
            throw new InvalidOperationException("The refresh token is invalid or expired.");

        return Task.FromResult(new GraphOAuthTokenResponse(
            AccessToken,
            RefreshToken,
            DateTimeOffset.UtcNow.AddHours(1)));
    }
}

