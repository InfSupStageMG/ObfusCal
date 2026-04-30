using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "tenant-id-123",
                ["AzureAd:ClientId"] = "client-id-456"
            })
            .Build();
        var options = Options.Create(new GraphConsentOptions
        {
            Scope = "https://graph.microsoft.com/Calendars.Read offline_access",
            ClientId = "consent-client-id"
        });

        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, config, options,
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

        Assert.IsTrue(url.StartsWith("https://login.microsoftonline.com/"),
            "URL should start with the configured Azure AD instance");
        Assert.IsTrue(url.Contains("tenant-id-123"), "URL should contain tenant ID");
        Assert.IsTrue(url.Contains("oauth2/v2.0/authorize"), "URL should point to authorize endpoint");
        Assert.IsTrue(url.Contains("client_id="), "URL should contain client_id parameter");
        Assert.IsTrue(url.Contains("redirect_uri="), "URL should contain redirect_uri parameter");
        Assert.IsTrue(url.Contains("scope="), "URL should contain scope parameter");
        Assert.IsTrue(url.Contains("response_type=code"), "URL should request authorization code");
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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "tenant-123",
                ["AzureAd:ClientId"] = "azure-ad-client"
            })
            .Build();
        var options = Options.Create(new GraphConsentOptions
        {
            ClientId = null, // force fallback to AzureAd:ClientId
            Scope = "openid"
        });

        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, config, options, new FakeGraphOAuthTokenClient());

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("azure-ad-client"),
            "Should fall back to AzureAd:ClientId when GraphConsent:ClientId is null");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_UsesDefaultScope_WhenScopeIsEmpty()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var dpProvider = DataProtectionProvider.Create("test");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "tenant-123",
                ["AzureAd:ClientId"] = "client-123"
            })
            .Build();
        var options = Options.Create(new GraphConsentOptions
        {
            ClientId = "client-123",
            Scope = "" // empty = use default
        });

        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, config, options, new FakeGraphOAuthTokenClient());

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("Calendars.Read"),
            "Should use default scope when configured scope is empty");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_UsesCustomScope_WhenScopeIsConfigured()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var dpProvider = DataProtectionProvider.Create("test");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "tenant-123",
                ["AzureAd:ClientId"] = "client-123"
            })
            .Build();
        var options = Options.Create(new GraphConsentOptions
        {
            ClientId = "client-123",
            Scope = "custom.scope offline_access"
        });

        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, config, options, new FakeGraphOAuthTokenClient());

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("custom.scope"),
            "Should use configured scope when provided");
        Assert.IsFalse(url.Contains("Calendars.Read"),
            "Should NOT use default scope when custom scope is configured");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_TrimsTrailingSlashFromInstance()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var dpProvider = DataProtectionProvider.Create("test");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com///",
                ["AzureAd:TenantId"] = "my-tenant",
                ["AzureAd:ClientId"] = "my-client"
            })
            .Build();
        var options = Options.Create(new GraphConsentOptions { ClientId = "my-client", Scope = "openid" });

        var svc = new CalendarOwnerGraphConsentService(
            db, dpProvider, config, options, new FakeGraphOAuthTokenClient());

        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        // With TrimEnd('/'), the URL should have exactly one slash between instance and tenant
        Assert.IsTrue(url.Contains("microsoftonline.com/my-tenant/oauth2"),
            "Instance trailing slashes should be trimmed");
    }

    [TestMethod]
    public void BuildAuthorizationUrl_ContainsResponseModeQuery()
    {
        var (svc, _, _) = Setup();
        var url = svc.BuildAuthorizationUrl("https://localhost/callback");

        Assert.IsTrue(url.Contains("response_mode=query"), "URL should contain response_mode=query");
        Assert.IsTrue(url.Contains("response_type=code"), "URL should contain response_type=code");
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
    public async Task HasConsentAsync_WithBothTokensNull_ReturnsFalse()
    {
        var (svc, _, ownerId) = Setup();
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

    private sealed class NullRefreshTokenClient : IGraphOAuthTokenClient
    {
        public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
            string authorizationCode, string redirectUri, CancellationToken ct = default)
        {
            return Task.FromResult(new GraphOAuthTokenResponse(
                "access-token",
                null, // no refresh token
                DateTimeOffset.UtcNow.AddHours(1)));
        }
    }

    private sealed class WhitespaceRefreshTokenClient : IGraphOAuthTokenClient
    {
        public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
            string authorizationCode, string redirectUri, CancellationToken ct = default)
        {
            return Task.FromResult(new GraphOAuthTokenResponse(
                "access-token",
                "   ", // whitespace-only refresh token
                DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}

