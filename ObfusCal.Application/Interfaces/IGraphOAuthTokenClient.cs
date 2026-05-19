namespace ObfusCal.Application.Interfaces;

public interface IGraphOAuthTokenClient
{
    Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
        string authorizationCode,
        string redirectUri,
        string? scope = null,
        CancellationToken ct = default);

    Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        string? scope = null,
        CancellationToken ct = default);
}

public sealed record GraphOAuthTokenResponse(
    string AccessToken,
    string? RefreshToken,
    string? Scope,
    DateTimeOffset? ExpiresAtUtc);

