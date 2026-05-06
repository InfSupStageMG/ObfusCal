using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class CalendarOwnerGraphConsentService(
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    ISecretProvider secretProvider,
    IOptions<GraphConsentOptions> graphConsentOptions,
    ICalendarSourceInstanceService calendarSourceInstanceService,
    ICalendarSourceInstanceStore calendarSourceInstanceStore,
    IGraphOAuthTokenClient tokenClient)
    : ICalendarOwnerGraphConsentService
{
    private const string GraphPluginId = "graph";

    private readonly IDataProtector _tokenProtector = dataProtectionProvider
        .CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

    public async Task<CalendarOwnerGraphConsentStatus?> GetStatusAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId, GraphPluginId, ct);
        if (instance is not null)
            return await GetStatusAsync(calendarOwnerId, instance.Id, ct);

        return await dbContext.CalendarOwners
            .AsNoTracking()
            .Where(owner => owner.Id == calendarOwnerId)
            .Select(owner => new CalendarOwnerGraphConsentStatus(
                !string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected)
                || !string.IsNullOrWhiteSpace(owner.GraphRefreshTokenProtected),
                owner.GraphConsentGrantedAtUtc,
                owner.GraphTokenExpiresAtUtc,
                owner.GraphTokenLastRefreshedAtUtc))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<CalendarOwnerGraphConsentStatus?> GetStatusAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct);
        if (instance is null || !string.Equals(instance.PluginId, GraphPluginId, StringComparison.OrdinalIgnoreCase))
            return null;

        var secretData = ParseSecretData(instance.SecretDataJson);
        return new CalendarOwnerGraphConsentStatus(
            !string.IsNullOrWhiteSpace(secretData?.ProtectedAccessToken)
            || !string.IsNullOrWhiteSpace(secretData?.ProtectedRefreshToken),
            secretData?.ConsentGrantedAtUtc,
            secretData?.TokenExpiresAtUtc,
            secretData?.TokenLastRefreshedAtUtc);
    }

    public async Task<bool> HasConsentAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId, GraphPluginId, ct);
        if (instance is not null)
            return await HasConsentAsync(calendarOwnerId, instance.Id, ct);

        return await dbContext.CalendarOwners
            .AsNoTracking()
            .Where(owner => owner.Id == calendarOwnerId)
            .AnyAsync(owner => !string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected)
                               || !string.IsNullOrWhiteSpace(owner.GraphRefreshTokenProtected), ct);
    }

    public async Task<bool> HasConsentAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(calendarOwnerId, calendarSourceInstanceId, ct);
        return status?.HasGraphConsent == true;
    }

    public string BuildAuthorizationUrl(string redirectUri)
        => BuildAuthorizationUrlCore(redirectUri);

    public async Task<string> BuildAuthorizationUrlAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string redirectUri,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct);
        if (instance is null || !string.Equals(instance.PluginId, GraphPluginId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Graph calendar source instance was not found.");

        return BuildAuthorizationUrlCore(redirectUri);
    }

    private string BuildAuthorizationUrlCore(string redirectUri)
    {
        // Validate that redirectUri is an absolute URI
        try
        {
            var uri = new Uri(redirectUri, UriKind.Absolute);
            // Ensure it's using http or https scheme
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Redirect URI must use http or https scheme.");
        }
        catch (UriFormatException ex)
        {
            throw new InvalidOperationException("Redirect URI must be an absolute URI.", ex);
        }

        var instance = (secretProvider.GetSecret("AzureAd:Instance") ?? "https://login.microsoftonline.com/").TrimEnd('/');
        var tenantId = secretProvider.GetSecret(SecretKeys.AzureAdTenantId)
            ?? throw new InvalidOperationException("AzureAd:TenantId is required.");
        var clientId = graphConsentOptions.Value.ClientId
            ?? secretProvider.GetSecret(SecretKeys.GraphConsentClientId)
            ?? secretProvider.GetSecret(SecretKeys.AzureAdClientId)
            ?? throw new InvalidOperationException("GraphConsent:ClientId or AzureAd:ClientId is required.");

        var scope = string.IsNullOrWhiteSpace(graphConsentOptions.Value.Scope)
            ? "https://graph.microsoft.com/Calendars.Read offline_access"
            : graphConsentOptions.Value.Scope;

        var query = string.Join("&",
            $"client_id={Uri.EscapeDataString(clientId)}",
            "response_type=code",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "response_mode=query",
            $"scope={Uri.EscapeDataString(scope)}",
            "prompt=consent");

        return $"{instance}/{tenantId}/oauth2/v2.0/authorize?{query}";
    }

    public async Task CompleteConsentAsync(
        Guid calendarOwnerId,
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default)
    {
        var owner = await dbContext.CalendarOwners
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId, ct)
            ?? throw new InvalidOperationException("Calendar owner was not found.");

        var tokenResponse = await tokenClient.ExchangeAuthorizationCodeAsync(authorizationCode, redirectUri, ct);

        owner.GraphAccessTokenProtected = _tokenProtector.Protect(tokenResponse.AccessToken);
        owner.GraphRefreshTokenProtected = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
            ? null
            : _tokenProtector.Protect(tokenResponse.RefreshToken);
        owner.GraphConsentGrantedAtUtc = DateTimeOffset.UtcNow;
        owner.GraphTokenLastRefreshedAtUtc = DateTimeOffset.UtcNow;
        owner.GraphTokenExpiresAtUtc = tokenResponse.ExpiresAtUtc;

        await dbContext.SaveChangesAsync(ct);

        var instance = await EnsureDefaultGraphInstanceAsync(calendarOwnerId, ct);
        if (instance is not null)
        {
            await calendarSourceInstanceService.UpdateAsync(
                calendarOwnerId,
                instance.Id,
                new UpdateCalendarSourceInstanceInput(
                    SecretDataJson: SerializeSecretData(new GraphCalendarSource.GraphSourceSecretData(
                        owner.GraphAccessTokenProtected,
                        owner.GraphRefreshTokenProtected,
                        owner.GraphConsentGrantedAtUtc,
                        owner.GraphTokenExpiresAtUtc,
                        owner.GraphTokenLastRefreshedAtUtc)),
                    IsEnabled: true),
                ct);
        }
    }

    public async Task CompleteConsentAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct)
            ?? throw new InvalidOperationException("Graph calendar source instance was not found.");

        if (!string.Equals(instance.PluginId, GraphPluginId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The specified calendar source instance is not a Graph source.");

        var tokenResponse = await tokenClient.ExchangeAuthorizationCodeAsync(authorizationCode, redirectUri, ct);
        var secretData = new GraphCalendarSource.GraphSourceSecretData(
            _tokenProtector.Protect(tokenResponse.AccessToken),
            string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
                ? null
                : _tokenProtector.Protect(tokenResponse.RefreshToken),
            DateTimeOffset.UtcNow,
            tokenResponse.ExpiresAtUtc,
            DateTimeOffset.UtcNow);

        var updated = await calendarSourceInstanceService.UpdateAsync(
            calendarOwnerId,
            calendarSourceInstanceId,
            new UpdateCalendarSourceInstanceInput(
                SecretDataJson: SerializeSecretData(secretData),
                IsEnabled: true),
            ct);

        if (updated is null)
            throw new InvalidOperationException("Graph calendar source instance was not found.");
    }

    private async Task<CalendarSourceInstanceContext?> EnsureDefaultGraphInstanceAsync(Guid calendarOwnerId, CancellationToken ct)
    {
        var existing = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId, GraphPluginId, ct);
        if (existing is not null)
            return existing;

        var created = await calendarSourceInstanceService.CreateAsync(
            calendarOwnerId,
            new CreateCalendarSourceInstanceInput(GraphPluginId, "Microsoft Graph"),
            ct);

        return created is null
            ? null
            : await calendarSourceInstanceStore.GetAsync(calendarOwnerId, created.Id, ct);
    }

    private static GraphCalendarSource.GraphSourceSecretData? ParseSecretData(string? secretDataJson)
    {
        if (string.IsNullOrWhiteSpace(secretDataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GraphCalendarSource.GraphSourceSecretData>(secretDataJson);
        }
        catch
        {
            return null;
        }
    }

    private static string SerializeSecretData(GraphCalendarSource.GraphSourceSecretData secretData)
        => JsonSerializer.Serialize(secretData);
}

