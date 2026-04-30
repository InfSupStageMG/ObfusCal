using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class GraphOAuthTokenClient(
    HttpClient httpClient,
    IConfiguration configuration,
    Microsoft.Extensions.Options.IOptions<GraphConsentOptions> graphConsentOptions)
    : IGraphOAuthTokenClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new InvalidOperationException("Authorization code is required to exchange Graph consent.");

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
            throw new InvalidOperationException("A valid absolute redirect URI is required to exchange Graph consent.");

        var oauthSettings = BuildOAuthSettings();

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = oauthSettings.ClientId,
            ["code"] = authorizationCode,
            ["redirect_uri"] = redirectUri,
            ["scope"] = oauthSettings.Scope
        };

        if (!string.IsNullOrWhiteSpace(oauthSettings.ClientSecret))
        {
            form["client_secret"] = oauthSettings.ClientSecret;
        }

        return await RequestTokenAsync(oauthSettings.TokenEndpoint, form, ct);
    }

    public async Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Refresh token is required to renew Graph access.");

        var oauthSettings = BuildOAuthSettings();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = oauthSettings.ClientId,
            ["refresh_token"] = refreshToken,
            ["scope"] = oauthSettings.Scope
        };

        if (!string.IsNullOrWhiteSpace(oauthSettings.ClientSecret))
        {
            form["client_secret"] = oauthSettings.ClientSecret;
        }

        return await RequestTokenAsync(oauthSettings.TokenEndpoint, form, ct);
    }

    private async Task<GraphOAuthTokenResponse> RequestTokenAsync(
        string tokenEndpoint,
        IReadOnlyDictionary<string, string> form,
        CancellationToken ct)
    {
        using var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Graph token exchange failed with HTTP {(int)response.StatusCode}: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Graph token exchange returned an empty payload.");

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
            throw new InvalidOperationException("Graph token exchange succeeded but did not return an access token.");

        DateTimeOffset? expiresAt = null;
        if (payload.ExpiresIn is > 0)
        {
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn.Value);
        }

        return new GraphOAuthTokenResponse(payload.AccessToken, payload.RefreshToken, expiresAt);
    }

    private OAuthSettings BuildOAuthSettings()
    {
        var azureAdSection = configuration.GetSection("AzureAd");
        var instance = (azureAdSection["Instance"] ?? "https://login.microsoftonline.com/").TrimEnd('/');
        var tenantId = azureAdSection["TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId is required.");
        var clientId = graphConsentOptions.Value.ClientId
            ?? azureAdSection["ClientId"]
            ?? throw new InvalidOperationException("GraphConsent:ClientId or AzureAd:ClientId is required.");

        var scope = string.IsNullOrWhiteSpace(graphConsentOptions.Value.Scope)
            ? "https://graph.microsoft.com/Calendars.Read offline_access"
            : graphConsentOptions.Value.Scope;

        return new OAuthSettings(
            $"{instance}/{tenantId}/oauth2/v2.0/token",
            clientId,
            graphConsentOptions.Value.ClientSecret,
            scope);
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


