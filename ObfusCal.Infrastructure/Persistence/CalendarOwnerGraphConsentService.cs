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
    private const string StatePrefix = "graph.";

    private readonly IDataProtector _tokenProtector = dataProtectionProvider
        .CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

    private readonly IDataProtector _stateProtector = dataProtectionProvider
        .CreateProtector("ObfusCal.GraphConsent.State.v1");

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
                ResolveAccessLevel(owner.GraphGrantedScopes),
                AllowsWriteBack(owner.GraphGrantedScopes),
                owner.GraphGrantedScopes,
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
            ResolveAccessLevel(secretData?.GrantedScopes),
            AllowsWriteBack(secretData?.GrantedScopes),
            secretData?.GrantedScopes,
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
        => BuildAuthorizationUrl(redirectUri, GraphConsentAccessLevel.ReadWrite);

    public string BuildAuthorizationUrl(string redirectUri, GraphConsentAccessLevel accessLevel)
        => BuildAuthorizationUrlCore(redirectUri, Guid.Empty, Guid.Empty, accessLevel);

    public async Task<string> BuildAuthorizationUrlAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string redirectUri,
        GraphConsentAccessLevel accessLevel,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct);
        if (instance is null || !string.Equals(instance.PluginId, GraphPluginId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Graph calendar source instance was not found.");

        return BuildAuthorizationUrlCore(redirectUri, calendarOwnerId, calendarSourceInstanceId, accessLevel);
    }

    private string BuildAuthorizationUrlCore(
        string redirectUri,
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        GraphConsentAccessLevel accessLevel)
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
        var authorityTenant = string.IsNullOrWhiteSpace(graphConsentOptions.Value.AuthorityTenant)
            ? null
            : graphConsentOptions.Value.AuthorityTenant;
        var tenantId = authorityTenant
            ?? secretProvider.GetSecret(SecretKeys.AzureAdTenantId)
            ?? throw new InvalidOperationException("AzureAd:TenantId is required.");
        var clientId = graphConsentOptions.Value.ClientId
            ?? secretProvider.GetSecret(SecretKeys.GraphConsentClientId)
            ?? secretProvider.GetSecret(SecretKeys.AzureAdClientId)
            ?? throw new InvalidOperationException("GraphConsent:ClientId or AzureAd:ClientId is required.");

        var scope = ResolveScope(accessLevel);

        var query = string.Join("&",
            $"client_id={Uri.EscapeDataString(clientId)}",
            "response_type=code",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "response_mode=query",
            $"scope={Uri.EscapeDataString(scope)}",
            "prompt=consent",
            $"state={Uri.EscapeDataString(BuildStateToken(calendarOwnerId, calendarSourceInstanceId, redirectUri, accessLevel, scope))}");

        return $"{instance}/{tenantId}/oauth2/v2.0/authorize?{query}";
    }

    public async Task<Guid> CompleteConsentFromStateAsync(
        string authorizationCode,
        string state,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new InvalidOperationException("State is required to complete Graph consent.");

        if (!state.StartsWith(StatePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Graph consent state is invalid.");

        var payload = UnprotectState(state);

        if (payload.ExpiresAtUtc < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Graph consent state has expired. Start consent again.");

        if (payload.CalendarOwnerId == Guid.Empty)
            throw new InvalidOperationException(
                "Graph consent state does not contain owner context. Use the instance-specific consent flow.");

        if (payload.CalendarSourceInstanceId != Guid.Empty)
        {
            await CompleteConsentAsync(
                payload.CalendarOwnerId,
                payload.CalendarSourceInstanceId,
                authorizationCode,
                payload.RedirectUri,
                payload.RequestedScope,
                ct);
        }
        else
        {
            await CompleteConsentAsync(
                payload.CalendarOwnerId,
                authorizationCode,
                payload.RedirectUri,
                payload.RequestedScope,
                ct);
        }

        return payload.CalendarOwnerId;
    }

    private string BuildStateToken(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string redirectUri,
        GraphConsentAccessLevel accessLevel,
        string requestedScope)
    {
        var payload = new GraphConsentStatePayload(
            calendarOwnerId,
            calendarSourceInstanceId,
            redirectUri,
            accessLevel,
            requestedScope,
            DateTimeOffset.UtcNow.AddMinutes(10));

        return StatePrefix + _stateProtector.Protect(JsonSerializer.Serialize(payload));
    }

    private GraphConsentStatePayload UnprotectState(string state)
    {
        var encrypted = state[StatePrefix.Length..];
        try
        {
            var json = _stateProtector.Unprotect(encrypted);
            return JsonSerializer.Deserialize<GraphConsentStatePayload>(json)
                ?? throw new InvalidOperationException("Graph consent state is invalid.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Graph consent state is invalid or expired.", ex);
        }
    }

    public Task CompleteConsentAsync(
        Guid calendarOwnerId,
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default)
        => CompleteConsentAsync(calendarOwnerId, authorizationCode, redirectUri, requestedScope: null, ct);

    private async Task CompleteConsentAsync(
        Guid calendarOwnerId,
        string authorizationCode,
        string redirectUri,
        string? requestedScope,
        CancellationToken ct = default)
    {
        var owner = await dbContext.CalendarOwners
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId, ct)
            ?? throw new InvalidOperationException("Calendar owner was not found.");

        var scope = string.IsNullOrWhiteSpace(requestedScope)
            ? ResolveScope(GraphConsentAccessLevel.ReadWrite)
            : requestedScope;
        var tokenResponse = await tokenClient.ExchangeAuthorizationCodeAsync(authorizationCode, redirectUri, scope, ct);

        owner.GraphAccessTokenProtected = _tokenProtector.Protect(tokenResponse.AccessToken);
        owner.GraphRefreshTokenProtected = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
            ? null
            : _tokenProtector.Protect(tokenResponse.RefreshToken);
        owner.GraphConsentGrantedAtUtc = DateTimeOffset.UtcNow;
        owner.GraphTokenLastRefreshedAtUtc = DateTimeOffset.UtcNow;
        owner.GraphTokenExpiresAtUtc = tokenResponse.ExpiresAtUtc;
        owner.GraphGrantedScopes = tokenResponse.Scope ?? scope;

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
                        owner.GraphGrantedScopes,
                        owner.GraphConsentGrantedAtUtc,
                        owner.GraphTokenExpiresAtUtc,
                        owner.GraphTokenLastRefreshedAtUtc)),
                    IsEnabled: true),
                ct);
        }
    }

    public Task CompleteConsentAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default)
        => CompleteConsentAsync(calendarOwnerId, calendarSourceInstanceId, authorizationCode, redirectUri, requestedScope: null, ct);

    private async Task CompleteConsentAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string authorizationCode,
        string redirectUri,
        string? requestedScope,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct)
            ?? throw new InvalidOperationException("Graph calendar source instance was not found.");

        if (!string.Equals(instance.PluginId, GraphPluginId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The specified calendar source instance is not a Graph source.");

        var scope = string.IsNullOrWhiteSpace(requestedScope)
            ? ResolveScope(GraphConsentAccessLevel.ReadWrite)
            : requestedScope;
        var tokenResponse = await tokenClient.ExchangeAuthorizationCodeAsync(authorizationCode, redirectUri, scope, ct);
        var secretData = new GraphCalendarSource.GraphSourceSecretData(
            _tokenProtector.Protect(tokenResponse.AccessToken),
            string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
                ? null
                : _tokenProtector.Protect(tokenResponse.RefreshToken),
            tokenResponse.Scope ?? scope,
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

    private string ResolveScope(GraphConsentAccessLevel accessLevel)
    {
        var options = graphConsentOptions.Value;

        const string defaultReadWriteScope = "https://graph.microsoft.com/Calendars.ReadWrite offline_access";
        if (accessLevel == GraphConsentAccessLevel.ReadWrite
            && !string.IsNullOrWhiteSpace(options.Scope)
            && !string.Equals(options.Scope, defaultReadWriteScope, StringComparison.OrdinalIgnoreCase))
        {
            return options.Scope;
        }

        var configuredScope = accessLevel == GraphConsentAccessLevel.ReadOnly
            ? options.ReadOnlyScope
            : options.ReadWriteScope;

        if (!string.IsNullOrWhiteSpace(configuredScope))
            return configuredScope;

        if (!string.IsNullOrWhiteSpace(options.Scope) && accessLevel == GraphConsentAccessLevel.ReadWrite)
            return options.Scope;

        return accessLevel == GraphConsentAccessLevel.ReadOnly
            ? "https://graph.microsoft.com/Calendars.Read offline_access"
            : "https://graph.microsoft.com/Calendars.ReadWrite offline_access";
    }

    private static bool AllowsWriteBack(string? scopes)
        => string.IsNullOrWhiteSpace(scopes)
           || scopes.Contains("Calendars.ReadWrite", StringComparison.OrdinalIgnoreCase);

    private static GraphConsentAccessLevel ResolveAccessLevel(string? scopes)
        => !string.IsNullOrWhiteSpace(scopes)
           && !AllowsWriteBack(scopes)
            ? GraphConsentAccessLevel.ReadOnly
            : GraphConsentAccessLevel.ReadWrite;

    private sealed record GraphConsentStatePayload(
        Guid CalendarOwnerId,
        Guid CalendarSourceInstanceId,
        string RedirectUri,
        GraphConsentAccessLevel AccessLevel,
        string RequestedScope,
        DateTimeOffset ExpiresAtUtc);
}
