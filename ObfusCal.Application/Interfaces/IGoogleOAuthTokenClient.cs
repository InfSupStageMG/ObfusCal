namespace ObfusCal.Application.Interfaces;

public interface IGoogleOAuthTokenClient
{
    Task<GoogleOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default);

    Task<GoogleOAuthTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken ct = default);
}

public sealed record GoogleOAuthTokenResponse(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAtUtc);

