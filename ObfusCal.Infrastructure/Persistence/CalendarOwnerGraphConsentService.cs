using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class CalendarOwnerGraphConsentService(
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    IConfiguration configuration,
    IOptions<GraphConsentOptions> graphConsentOptions,
    IGraphOAuthTokenClient tokenClient)
    : ICalendarOwnerGraphConsentService
{
    private readonly IDataProtector _tokenProtector = dataProtectionProvider
        .CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

    public async Task<CalendarOwnerGraphConsentStatus?> GetStatusAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
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

    public async Task<bool> HasConsentAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        return await dbContext.CalendarOwners
            .AsNoTracking()
            .Where(owner => owner.Id == calendarOwnerId)
            .AnyAsync(owner => !string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected)
                               || !string.IsNullOrWhiteSpace(owner.GraphRefreshTokenProtected), ct);
    }

    public string BuildAuthorizationUrl(string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
            throw new InvalidOperationException("Redirect URI must be an absolute URI.");

        var azureAdSection = configuration.GetSection("AzureAd");
        var instance = (azureAdSection["Instance"] ?? "https://login.microsoftonline.com/").TrimEnd('/');
        var tenantId = azureAdSection["TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId is required.");
        var clientId = graphConsentOptions.Value.ClientId
            ?? azureAdSection["ClientId"]
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
    }
}

