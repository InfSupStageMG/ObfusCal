using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class GoogleOAuthTokenClient(
    HttpClient httpClient,
    ISecretProvider secretProvider,
    ILogRedactor logRedactor,
    IOptions<GoogleConsentOptions> googleConsentOptions,
    ILogger<GoogleOAuthTokenClient> logger)
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
            ["redirect_uri"] = redirectUri
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
            logger.LogError("Google token exchange failed with HTTP {StatusCode}: {ErrorBody}", (int)response.StatusCode, logRedactor.Redact(body));
            throw new InvalidOperationException(BuildTokenExchangeFailureMessage((int)response.StatusCode, body, logRedactor.Redact(body)));
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
            clientSecret);
    }

    private static string BuildTokenExchangeFailureMessage(int statusCode, string body, string redactedBody)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var error = root.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : null;
            var description = root.TryGetProperty("error_description", out var descriptionProp) ? descriptionProp.GetString() : null;

            if (string.Equals(error, "invalid_grant", StringComparison.OrdinalIgnoreCase))
            {
                return "Google token exchange failed with invalid_grant. The authorization code may be expired or already used, or the redirect URI did not exactly match the URI used during authorization.";
            }

            if (!string.IsNullOrWhiteSpace(error) || !string.IsNullOrWhiteSpace(description))
            {
                return $"Google token exchange failed with HTTP {statusCode}: error='{error}', description='{description}'.";
            }
        }
        catch (JsonException)
        {
            // Keep the generic message when the provider does not return JSON.
        }

        return $"Google token exchange failed with HTTP {statusCode}: {redactedBody}";
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

    private sealed record OAuthSettings(string TokenEndpoint, string ClientId, string? ClientSecret);
}

