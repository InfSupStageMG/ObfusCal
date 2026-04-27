namespace ObfusCal.Application.Interfaces;

public interface IGraphOAuthTokenClient
{
    Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default);
}

public sealed record GraphOAuthTokenResponse(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAtUtc);

