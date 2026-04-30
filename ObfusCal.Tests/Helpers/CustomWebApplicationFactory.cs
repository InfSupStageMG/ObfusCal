using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Storage;
using Testcontainers.PostgreSql;

namespace ObfusCal.Tests.Helpers;

public sealed class CustomWebApplicationFactory(string environmentName, bool useTestAuthentication = false) : WebApplicationFactory<Program>
{
    public const string IntegrationTestPeerInstanceId = "peer-a";
    public const string IntegrationTestPeerApiKey = "integration-test-peer-api-key";

    private static readonly PostgreSqlContainer Postgres = new PostgreSqlBuilder("postgres:17").Build();

    static CustomWebApplicationFactory()
    {
        Postgres.StartAsync().GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);

        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = Postgres.GetConnectionString(),
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
                ["AzureAd:Domain"] = "infosupport.onmicrosoft.com",
                ["AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222",
                ["GraphConsent:ApiBaseUrl"] = "https://graph.microsoft.com",
                ["GraphConsent:Scope"] = "https://graph.microsoft.com/Calendars.Read offline_access",
                ["GraphConsent:ClientId"] = "33333333-3333-3333-3333-333333333333",
                ["GraphConsent:ClientSecret"] = "integration-test-secret",
                ["Swagger:OAuth:ClientId"] = "22222222-2222-2222-2222-222222222222",
                ["Swagger:OAuth:Scope"] = "api://22222222-2222-2222-2222-222222222222/access_as_user",
                ["Sync:InstanceId"] = "integration-test-local-instance",
                ["Sync:ApiKey"] = "integration-test-outbound-api-key",
                ["Sync:SyncIntervalSeconds"] = "3600",
                ["Sync:LookAheadDays"] = "14"
            }));

        builder.ConfigureServices(services =>
        {
            var storeDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IShadowSlotStore));
            if (storeDescriptor is not null)
                services.Remove(storeDescriptor);

            services.AddSingleton<IShadowSlotStore, InMemoryShadowSlotStore>();

            var graphTokenClientDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IGraphOAuthTokenClient));
            if (graphTokenClientDescriptor is not null)
                services.Remove(graphTokenClientDescriptor);

            services.AddSingleton<IGraphOAuthTokenClient, FakeGraphOAuthTokenClient>();

            if (!useTestAuthentication) return;
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.AddAuthorization();
        });
    }

    public HttpClient CreateAuthenticatedClient(string objectId = TestAuthHandler.DefaultObjectId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, objectId);
        return client;
    }

    public HttpClient CreateAuthenticatedClientWithRoles(
        string objectId = TestAuthHandler.DefaultObjectId,
        params string[] roles)
    {
        var client = CreateAuthenticatedClient(objectId);
        if (roles.Length > 0)
            client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
        return client;
    }

    public async Task<Guid> SeedCalendarOwnerAsync(
        string entraObjectId,
        Guid? calendarOwnerId = null,
        string? name = null)
    {
        var requestedId = calendarOwnerId ?? Guid.NewGuid();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingOwner = await dbContext.CalendarOwners
            .SingleOrDefaultAsync(o => o.Id == requestedId || o.EntraObjectId == entraObjectId);

        if (existingOwner is null)
        {
            var owner = new CalendarOwner
            {
                Id = requestedId,
                Name = name ?? "Integration Test Calendar Owner",
                EntraObjectId = entraObjectId
            };

            dbContext.CalendarOwners.Add(owner);
            await dbContext.SaveChangesAsync();

            return owner.Id;
        }

        existingOwner.Name = name ?? existingOwner.Name;
        existingOwner.EntraObjectId = entraObjectId;

        await dbContext.SaveChangesAsync();
        return existingOwner.Id;
    }

    public async Task GrantGraphConsentAsync(
        Guid calendarOwnerId,
        string authorizationCode = FakeGraphOAuthTokenClient.ValidAuthorizationCode,
        string redirectUri = "https://localhost/swagger/oauth2-redirect.html")
    {
        using var scope = Services.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ICalendarOwnerGraphConsentService>();
        await consentService.CompleteConsentAsync(calendarOwnerId, authorizationCode, redirectUri);
    }

    public async Task<CalendarOwner?> GetCalendarOwnerAsync(Guid calendarOwnerId)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.CalendarOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(owner => owner.Id == calendarOwnerId);
    }

    public async Task<Guid> SeedPeerConnectionAsync(
        string instanceId = IntegrationTestPeerInstanceId,
        string rawApiKey = IntegrationTestPeerApiKey,
        string baseAddress = "https://peer-a.local")
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingPeer = await dbContext.PeerConnections
            .SingleOrDefaultAsync(peer => peer.InstanceId == instanceId);

        var hasher = new PasswordHasher<PeerConnection>();

        if (existingPeer is null)
        {
            var peer = new PeerConnection
            {
                Id = Guid.NewGuid(),
                InstanceId = instanceId,
                BaseAddress = baseAddress,
                ApiKeyHash = string.Empty
            };

            peer.ApiKeyHash = hasher.HashPassword(peer, rawApiKey);
            dbContext.PeerConnections.Add(peer);
            await dbContext.SaveChangesAsync();
            return peer.Id;
        }

        existingPeer.BaseAddress = baseAddress;
        existingPeer.ApiKeyHash = hasher.HashPassword(existingPeer, rawApiKey);
        await dbContext.SaveChangesAsync();
        return existingPeer.Id;
    }

    public async Task SeedCalendarOwnerPeerMappingAsync(
        Guid calendarOwnerId,
        Guid calendarOwnerRef,
        string instanceId = IntegrationTestPeerInstanceId,
        string rawApiKey = IntegrationTestPeerApiKey)
    {
        var peerConnectionId = await SeedPeerConnectionAsync(instanceId, rawApiKey);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingMapping = await dbContext.CalendarOwnerPeerMappings
            .SingleOrDefaultAsync(mapping =>
                mapping.CalendarOwnerId == calendarOwnerId &&
                mapping.PeerConnectionId == peerConnectionId &&
                mapping.CalendarOwnerRef == calendarOwnerRef);

        if (existingMapping is null)
        {
            dbContext.CalendarOwnerPeerMappings.Add(new CalendarOwnerPeerMapping
            {
                Id = Guid.NewGuid(),
                CalendarOwnerId = calendarOwnerId,
                PeerConnectionId = peerConnectionId,
                CalendarOwnerRef = calendarOwnerRef
            });

            await dbContext.SaveChangesAsync();
        }
    }
}
