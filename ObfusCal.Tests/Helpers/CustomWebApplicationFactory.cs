using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Storage;
using Testcontainers.PostgreSql;

namespace ObfusCal.Tests.Helpers;

public sealed class CustomWebApplicationFactory(string environmentName, bool useTestAuthentication = false) : WebApplicationFactory<Program>
{
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
                ["Swagger:OAuth:ClientId"] = "22222222-2222-2222-2222-222222222222",
                ["Swagger:OAuth:Scope"] = "api://22222222-2222-2222-2222-222222222222/access_as_user"
            }));

        builder.ConfigureServices(services =>
        {
            var storeDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IShadowSlotStore));
            if (storeDescriptor is not null)
                services.Remove(storeDescriptor);

            services.AddSingleton<IShadowSlotStore, InMemoryShadowSlotStore>();

            if (useTestAuthentication)
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                        options.DefaultScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                services.AddAuthorization();
            }
        });
    }

    public HttpClient CreateAuthenticatedClient(string objectId = TestAuthHandler.DefaultObjectId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, objectId);
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
}
