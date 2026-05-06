using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class GoogleOAuthTokenClient(
    HttpClient httpClient,
    ISecretProvider secretProvider,
    ILogRedactor logRedactor,
    IOptions<GoogleConsentOptions> googleConsentOptions)
    : IGoogleOAuthTokenClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GoogleOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new InvalidOperationException("Authorization code is required to exchange Google consent.");

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
            throw new InvalidOperationException("A valid absolute redirect URI is required to exchange Google consent.");

        var settings = BuildOAuthSettings();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = settings.ClientId,
            ["code"] = authorizationCode,
            ["redirect_uri"] = redirectUri,
            ["scope"] = settings.Scope
        };

        if (!string.IsNullOrWhiteSpace(settings.ClientSecret))
            form["client_secret"] = settings.ClientSecret;

        return await RequestTokenAsync(settings.TokenEndpoint, form, ct);
    }

    public async Task<GoogleOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Refresh token is required to renew Google access.");

        var settings = BuildOAuthSettings();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = settings.ClientId,
            ["refresh_token"] = refreshToken
        };

        if (!string.IsNullOrWhiteSpace(settings.ClientSecret))
            form["client_secret"] = settings.ClientSecret;

        return await RequestTokenAsync(settings.TokenEndpoint, form, ct);
    }

    private async Task<GoogleOAuthTokenResponse> RequestTokenAsync(
        string tokenEndpoint,
        IReadOnlyDictionary<string, string> form,
        CancellationToken ct)
    {
        using var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Google token exchange failed with HTTP {(int)response.StatusCode}: {logRedactor.Redact(body)}");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Google token exchange returned an empty payload.");

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
            throw new InvalidOperationException("Google token exchange succeeded but did not return an access token.");

        DateTimeOffset? expiresAt = null;
        if (payload.ExpiresIn is > 0)
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn.Value);

        return new GoogleOAuthTokenResponse(payload.AccessToken, payload.RefreshToken, expiresAt);
    }

    private OAuthSettings BuildOAuthSettings()
    {
        var configClientId = string.IsNullOrWhiteSpace(googleConsentOptions.Value.ClientId) || IsPlaceholder(googleConsentOptions.Value.ClientId)
            ? null
            : googleConsentOptions.Value.ClientId;

        var clientId = configClientId
            ?? secretProvider.GetSecret(SecretKeys.GoogleConsentClientId)
            ?? throw new InvalidOperationException("GoogleConsent:ClientId is required. Set via environment variable GOOGLECONSENT__CLIENTID or configuration.");

        var scope = googleConsentOptions.Value.Scope.Trim();
        if (string.IsNullOrWhiteSpace(scope))
            throw new InvalidOperationException("GoogleConsent:Scope is required.");

        var tokenEndpoint = googleConsentOptions.Value.TokenEndpoint.Trim();
        if (string.IsNullOrWhiteSpace(tokenEndpoint))
            throw new InvalidOperationException("GoogleConsent:TokenEndpoint is required.");

        var configClientSecret = string.IsNullOrWhiteSpace(googleConsentOptions.Value.ClientSecret) || IsPlaceholder(googleConsentOptions.Value.ClientSecret)
            ? null
            : googleConsentOptions.Value.ClientSecret;

        var clientSecret = configClientSecret ?? secretProvider.GetSecret(SecretKeys.GoogleConsentClientSecret);

        return new OAuthSettings(
            tokenEndpoint,
            clientId,
            clientSecret,
            scope);
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return trimmed.StartsWith('[') && trimmed.EndsWith(']');
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")]
        string? AccessToken,
        [property: JsonPropertyName("refresh_token")]
        string? RefreshToken,
        [property: JsonPropertyName("expires_in")]
        int? ExpiresIn);

    private sealed record OAuthSettings(string TokenEndpoint, string ClientId, string? ClientSecret, string Scope);
}

