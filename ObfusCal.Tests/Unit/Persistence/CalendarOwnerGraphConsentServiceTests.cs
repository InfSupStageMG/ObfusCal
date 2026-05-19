using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Persistence;

[TestClass]
public class CalendarOwnerGraphConsentServiceTests
{
    private static (CalendarOwnerGraphConsentService service, AppDbContext db, Guid ownerId) Setup(
        IGraphOAuthTokenClient? tokenClient = null)
    {
        var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Test" });
        db.SaveChanges();

        var dpProvider = DataProtectionProvider.Create("test");
        var secretProvider = CreateSecretProvider(
            "https://login.microsoftonline.com/",
            "tenant-id-123",
            "client-id-456");
        var options = Options.Create(new GraphConsentOptions
        {
            Scope = "https://graph.microsoft.com/Calendars.ReadWrite offline_access",
            ClientId = "consent-client-id"
        });

        var instances = new FakeCalendarSourceInstanceService(calendarOwnerId => db.CalendarOwners.Any(owner => owner.Id == calendarOwnerId));
        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, secretProvider, options, instances, instances,
            tokenClient ?? new FakeGraphOAuthTokenClient());

        return (svc, db, ownerId);
    }

    [TestMethod]
    public async Task GetStatusAsync_ReturnsNull_WhenOwnerDoesNotExist()
    {
        var (svc, _, _) = Setup();
        var status = await svc.GetStatusAsync(Guid.NewGuid());
        Assert.IsNull(status);
    }

    [TestMethod]
    public async Task GetStatusAsync_ReturnsNoConsent_ForNewOwner()
    {
        var (svc, _, ownerId) = Setup();
        var status = await svc.GetStatusAsync(ownerId);

        Assert.IsNotNull(status);
        Assert.IsFalse(status.HasGraphConsent);
        Assert.IsNull(status.GrantedAtUtc);
        Assert.IsNull(status.TokenExpiresAtUtc);
        Assert.IsNull(status.TokenLastRefreshedAtUtc);
    }

    [TestMethod]
    public async Task HasConsentAsync_ReturnsFalse_ForNewOwner()
    {
        var (svc, _, ownerId) = Setup();
        var hasConsent = await svc.HasConsentAsync(ownerId);
        Assert.IsFalse(hasConsent);
    }

    [TestMethod]
    public async Task CompleteConsentAsync_StoresTokensAndUpdatesTimestamps()
    {
        var (svc, db, ownerId) = Setup();

        await svc.CompleteConsentAsync(ownerId,
            FakeGraphOAuthTokenClient.ValidAuthorizationCode,
            "https://localhost/callback");

        var owner = db.CalendarOwners.Single(o => o.Id == ownerId);
        Assert.IsNotNull(owner.GraphAccessTokenProtected, "Access token should be stored");
        Assert.IsNotNull(owner.GraphRefreshTokenProtected, "Refresh token should be stored");
        Assert.IsNotNull(owner.GraphConsentGrantedAtUtc, "Consent grant timestamp should be set");
        Assert.IsNotNull(owner.GraphTokenLastRefreshedAtUtc, "Token refresh timestamp should be set");
        Assert.IsNotNull(owner.GraphTokenExpiresAtUtc, "Token expiry should be set");
    }

    [TestMethod]
    public async Task CompleteConsentAsync_ThenHasConsent_ReturnsTrue()
    {
        var (svc, _, ownerId) = Setup();

        await svc.CompleteConsentAsync(ownerId,
            FakeGraphOAuthTokenClient.ValidAuthorizationCode,
            "https://localhost/callback");

        var hasConsent = await svc.HasConsentAsync(ownerId);
        Assert.IsTrue(hasConsent);
    }

    [TestMethod]
    public async Task CompleteConsentAsync_ThenGetStatus_ShowsConsent()
    {
        var (svc, _, ownerId) = Setup();

        await svc.CompleteConsentAsync(ownerId,
            FakeGraphOAuthTokenClient.ValidAuthorizationCode,
            "https://localhost/callback");

        var status = await svc.GetStatusAsync(ownerId);
        Assert.IsNotNull(status);
        Assert.IsTrue(status.HasGraphConsent);
        Assert.IsNotNull(status.GrantedAtUtc);
    }

    [TestMethod]
    public async Task CompleteConsentAsync_WithInvalidOwner_Throws()
    {
        var (svc, _, _) = Setup();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            svc.CompleteConsentAsync(Guid.NewGuid(),
                FakeGraphOAuthTokenClient.ValidAuthorizationCode,
                "https://localhost/callback"));
    }

    [TestMethod]
    public void BuildAuthorizationUrl_ReturnsValidUrl()
    {
        var (svc, _, _) = Setup();

        var url = svc.BuildAuthorizationUrl("https://localhost/swagger/oauth2-redirect.html");

        Assert.StartsWith("https://login.microsoftonline.com/", url,
            "URL should start with the configured Azure AD instance");
        Assert.Contains("tenant-id-123", url, "URL should contain tenant ID");
        Assert.Contains("oauth2/v2.0/authorize", url, "URL should point to authorize endpoint");
        Assert.Contains("client_id=", url, "URL should contain client_id parameter");
        Assert.Contains("redirect_uri=", url, "URL should contain redirect_uri parameter");
        Assert.Contains("scope=", url, "URL should contain scope parameter");
        Assert.Contains("response_type=code", url, "URL should request authorization code");
        Assert.Contains("state=", url, "URL should contain a state parameter for CSRF protection");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_WithRelativeUri_Throws()
    {
        var (svc, _, _) = Setup();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            svc.BuildAuthorizationUrl("/relative/path"));
    }

    [TestMethod]
    public void BuildAuthorizationUrl_UsesConsentClientId()
    {
        var (svc, _, _) = Setup();

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("consent-client-id"),
            "Should use GraphConsent:ClientId when available");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_IncludesPromptConsent()
    {
        var (svc, _, _) = Setup();

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("prompt=consent"), "URL should include prompt=consent");
    }

    [TestMethod]
    public async Task CompleteConsentAsync_WithNullRefreshToken_StoresNull()
    {
        var nullRefreshTokenClient = new NullRefreshTokenClient();
        var (svc, db, ownerId) = Setup(nullRefreshTokenClient);

        await svc.CompleteConsentAsync(ownerId, "valid-consent-code", "https://localhost/callback");

        var owner = db.CalendarOwners.Single(o => o.Id == ownerId);
        Assert.IsNotNull(owner.GraphAccessTokenProtected);
        Assert.IsNull(owner.GraphRefreshTokenProtected, "Null refresh token should store null");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_FallsBackToAzureAdClientId_WhenConsentClientIdIsNull()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var dpProvider = DataProtectionProvider.Create("test");
        var secretProvider = CreateSecretProvider(
            "https://login.microsoftonline.com/",
            "tenant-123",
            "azure-ad-client");
        var options = Options.Create(new GraphConsentOptions
        {
            ClientId = null, // force fallback to AzureAd:ClientId
            Scope = "openid"
        });

        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, secretProvider, options,
            new FakeCalendarSourceInstanceService(),
            new FakeCalendarSourceInstanceService(),
            new FakeGraphOAuthTokenClient());

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("azure-ad-client"),
            "Should fall back to AzureAd:ClientId when GraphConsent:ClientId is null");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_UsesDefaultScope_WhenScopeIsEmpty()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var dpProvider = DataProtectionProvider.Create("test");
        var secretProvider = CreateSecretProvider(
            "https://login.microsoftonline.com/",
            "tenant-123",
            "client-123");
        var options = Options.Create(new GraphConsentOptions
        {
            ClientId = "client-123",
            Scope = "" // empty = use default
        });

        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, secretProvider, options,
            new FakeCalendarSourceInstanceService(),
            new FakeCalendarSourceInstanceService(),
            new FakeGraphOAuthTokenClient());

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("Calendars.ReadWrite"),
            "Should use default scope when configured scope is empty");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_UsesCustomScope_WhenScopeIsConfigured()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var dpProvider = DataProtectionProvider.Create("test");
        var secretProvider = CreateSecretProvider(
            "https://login.microsoftonline.com/",
            "tenant-123",
            "client-123");
        var options = Options.Create(new GraphConsentOptions
        {
            ClientId = "client-123",
            Scope = "custom.scope offline_access"
        });

        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, secretProvider, options,
            new FakeCalendarSourceInstanceService(),
            new FakeCalendarSourceInstanceService(),
            new FakeGraphOAuthTokenClient());

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("custom.scope"),
            "Should use configured scope when provided");
        Assert.IsFalse(url.Contains("Calendars.ReadWrite"),
            "Should NOT use default scope when custom scope is configured");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_TrimsTrailingSlashFromInstance()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var dpProvider = DataProtectionProvider.Create("test");
        var secretProvider = CreateSecretProvider(
            "https://login.microsoftonline.com///",
            "my-tenant",
            "my-client");
        var options = Options.Create(new GraphConsentOptions { ClientId = "my-client", Scope = "openid" });

        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, secretProvider, options,
            new FakeCalendarSourceInstanceService(),
            new FakeCalendarSourceInstanceService(),
            new FakeGraphOAuthTokenClient());

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        // With TrimEnd('/'), the URL should have exactly one slash between instance and tenant
        Assert.Contains("microsoftonline.com/my-tenant/oauth2", url,
            "Instance trailing slashes should be trimmed");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_ContainsResponseModeQuery()
    {
        var (svc, _, _) = Setup();
        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.Contains("response_mode=query", url, "URL should contain response_mode=query");
        Assert.Contains("response_type=code", url, "URL should contain response_type=code");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_EmptyAuthorityTenant_FallsBackToAzureAdTenantId()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var dpProvider = DataProtectionProvider.Create("authority-tenant-empty");
        var secretProvider = CreateSecretProvider(
            "https://login.microsoftonline.com/",
            "tenant-from-azuread",
            "client-id");
        var options = Options.Create(new GraphConsentOptions
        {
            ClientId = "client-id",
            AuthorityTenant = ""
        });

        var instanceService = new FakeCalendarSourceInstanceService();
        var svc = new CalendarOwnerGraphConsentService(
            db,
            dpProvider,
            secretProvider,
            options,
            instanceService,
            instanceService,
            new FakeGraphOAuthTokenClient());

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("/tenant-from-azuread/oauth2/v2.0/authorize", StringComparison.Ordinal),
            "Empty AuthorityTenant should not produce a double-slash tenant segment in the Microsoft authorize URL.");
    }

    [TestMethod]
    public async Task BuildAuthorizationUrlAsync_IncludesStatePrefixedWithGraph()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Test" });
        await db.SaveChangesAsync();

        var instanceSvc = new FakeCalendarSourceInstanceService(id => db.CalendarOwners.Any(o => o.Id == id));
        var dpProvider = DataProtectionProvider.Create("state-prefix-test");
        var consoleSvc = new CalendarOwnerGraphConsentService(
            db, dpProvider,
            CreateSecretProvider("https://login.microsoftonline.com/", "tenant-x", "client-x"),
            Options.Create(new GraphConsentOptions { Scope = "openid", ClientId = "client-x" }),
            instanceSvc, instanceSvc, new FakeGraphOAuthTokenClient());

        var created = await instanceSvc.CreateAsync(ownerId, new CreateCalendarSourceInstanceInput("graph", "Microsoft Graph"));
        var instanceId = created!.Id;

        var url = await consoleSvc.BuildAuthorizationUrlAsync(ownerId, instanceId, "https://localhost/consent-callback", GraphConsentAccessLevel.ReadWrite);

        // The state is URI-escaped in the URL; the "graph." prefix survives escaping as-is
        Assert.Contains("state=graph.", url,
            "Graph authorization URL must include a 'graph.'-prefixed state token");
    }

    [TestMethod]
    public async Task CompleteConsentFromStateAsync_RoundTrip_CompletesConsent()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Test" });
        await db.SaveChangesAsync();

        var instanceSvc = new FakeCalendarSourceInstanceService(id => db.CalendarOwners.Any(o => o.Id == id));
        var dpProvider = DataProtectionProvider.Create("roundtrip-test");
        var consoleSvc = new CalendarOwnerGraphConsentService(
            db, dpProvider,
            CreateSecretProvider("https://login.microsoftonline.com/", "t", "c"),
            Options.Create(new GraphConsentOptions { Scope = "openid", ClientId = "c" }),
            instanceSvc, instanceSvc, new FakeGraphOAuthTokenClient());

        // Create the graph source instance
        var created = await instanceSvc.CreateAsync(ownerId, new CreateCalendarSourceInstanceInput("graph", "Microsoft Graph"));
        var instanceId = created!.Id;

        // Build the authorization URL (which embeds calendarOwnerId + instanceId in the state)
        var url = await consoleSvc.BuildAuthorizationUrlAsync(ownerId, instanceId, "https://localhost/consent-callback", GraphConsentAccessLevel.ReadWrite);

        // Extract the raw state value from the URL
        var rawState = url.Split("state=")[1].Split('&')[0];
        var state = Uri.UnescapeDataString(rawState);

        Assert.IsTrue(state.StartsWith("graph.", StringComparison.Ordinal),
            "State must start with 'graph.' prefix");

        // Complete consent using the state - no owner/instance IDs needed by the caller
        var returnedOwnerId = await consoleSvc.CompleteConsentFromStateAsync(FakeGraphOAuthTokenClient.ValidAuthorizationCode, state);

        Assert.AreEqual(ownerId, returnedOwnerId, "CompleteConsentFromStateAsync should return the calendar owner ID");

        // Verify the token was stored in the source instance
        var stored = await instanceSvc.GetAsync(ownerId, instanceId);
        Assert.IsNotNull(stored?.SecretDataJson, "Secret data should be stored after consent completion");
    }

    [TestMethod]
    public async Task CompleteConsentFromStateAsync_WithInvalidToken_Throws()
    {
        var (svc, _, _) = Setup();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            svc.CompleteConsentFromStateAsync("code", "graph.this-is-not-a-valid-encrypted-payload"));
    }

    [TestMethod]
    public async Task CompleteConsentFromStateAsync_WithoutGraphPrefix_Throws()
    {
        var (svc, _, _) = Setup();

        // A state without "graph." prefix must be rejected by the Graph service
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            svc.CompleteConsentFromStateAsync("code", "google-or-other-provider-state-token"));
    }

    [TestMethod]
    public async Task CompleteConsentFromStateAsync_WithLegacyNonOwnerState_Throws()
    {
        // The non-async BuildAuthorizationUrl produces a state with Guid.Empty owner.
        // CompleteConsentFromStateAsync must reject it with a clear message.
        var db = TestDbContextFactory.CreateInMemory();
        var dpProvider = DataProtectionProvider.Create("empty-owner-test");
        var options = Options.Create(new GraphConsentOptions { Scope = "openid", ClientId = "c" });
        var instanceService = new FakeCalendarSourceInstanceService();
        var consoleSvc = new CalendarOwnerGraphConsentService(
            db, dpProvider,
            CreateSecretProvider("https://login.microsoftonline.com/", "t", "c"),
            options, instanceService, instanceService, new FakeGraphOAuthTokenClient());

        var url = consoleSvc.BuildAuthorizationUrl("https://localhost/consent-callback");
        var rawState = url.Split("state=")[1].Split('&')[0];
        var state = Uri.UnescapeDataString(rawState);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            consoleSvc.CompleteConsentFromStateAsync("code", state));

        Assert.IsTrue(ex.Message.Contains("owner context", StringComparison.OrdinalIgnoreCase),
            "Error should explain that the legacy flow lacks owner context");
    }

    [TestMethod]
    public async Task GetStatusAsync_WithAccessTokenOnly_ReturnsHasConsent()
    {
        var (svc, db, ownerId) = Setup();

        // Manually set only access token
        var owner = db.CalendarOwners.Single(o => o.Id == ownerId);
        owner.GraphAccessTokenProtected = "some-protected-token";
        owner.GraphRefreshTokenProtected = null;
        await db.SaveChangesAsync();

        var status = await svc.GetStatusAsync(ownerId);

        Assert.IsNotNull(status);
        Assert.IsTrue(status.HasGraphConsent, "Should have consent when access token is present");
    }

    [TestMethod]
    public async Task HasConsentAsync_WithRefreshTokenOnly_ReturnsTrue()
    {
        var (svc, db, ownerId) = Setup();

        // Manually set only refresh token
        var owner = db.CalendarOwners.Single(o => o.Id == ownerId);
        owner.GraphAccessTokenProtected = null;
        owner.GraphRefreshTokenProtected = "some-refresh-token";
        await db.SaveChangesAsync();

        var hasConsent = await svc.HasConsentAsync(ownerId);

        Assert.IsTrue(hasConsent, "Should have consent when refresh token is present");
    }

    [TestMethod]
    public async Task HasConsentAsync_WithTokensExplicitlyCleared_ReturnsFalse()
    {
        var (svc, db, ownerId) = Setup();

        var owner = db.CalendarOwners.Single(o => o.Id == ownerId);
        owner.GraphAccessTokenProtected = "token";
        owner.GraphRefreshTokenProtected = "refresh";
        await db.SaveChangesAsync();

        owner.GraphAccessTokenProtected = null;
        owner.GraphRefreshTokenProtected = null;
        await db.SaveChangesAsync();

        var hasConsent = await svc.HasConsentAsync(ownerId);
        Assert.IsFalse(hasConsent);
    }

    [TestMethod]
    public async Task CompleteConsentAsync_WithWhitespaceRefreshToken_StoresNull()
    {
        var whitespaceClient = new WhitespaceRefreshTokenClient();
        var (svc, db, ownerId) = Setup(whitespaceClient);

        await svc.CompleteConsentAsync(ownerId, "code", "https://localhost/callback");

        var owner = db.CalendarOwners.Single(o => o.Id == ownerId);
        Assert.IsNull(owner.GraphRefreshTokenProtected,
            "Whitespace-only refresh token should be stored as null");
    }

    private static ISecretProvider CreateSecretProvider(string instance, string tenantId, string clientId)
        => new DictionarySecretProvider(new Dictionary<string, string?>
        {
            ["AzureAd:Instance"] = instance,
            ["AzureAd:TenantId"] = tenantId,
            ["AzureAd:ClientId"] = clientId
        });

    private sealed class NullRefreshTokenClient : IGraphOAuthTokenClient
    {
        public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
            string authorizationCode, string redirectUri, string? scope = null, CancellationToken ct = default)
        {
            return Task.FromResult(new GraphOAuthTokenResponse(
                "access-token",
                null, // no refresh token
                scope,
                DateTimeOffset.UtcNow.AddHours(1)));
        }

        public Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(
            string refreshToken, string? scope = null, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class WhitespaceRefreshTokenClient : IGraphOAuthTokenClient
    {
        public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
            string authorizationCode, string redirectUri, string? scope = null, CancellationToken ct = default)
        {
            return Task.FromResult(new GraphOAuthTokenResponse(
                "access-token",
                "   ", // whitespace-only refresh token
                scope,
                DateTimeOffset.UtcNow.AddHours(1)));
        }

        public Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(
            string refreshToken, string? scope = null, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class DictionarySecretProvider(IReadOnlyDictionary<string, string?> values) : ISecretProvider
    {
        public string? GetSecret(string key) => values.TryGetValue(key, out var value) ? value : null;
    }
}
