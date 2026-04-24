using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Storage;
using Testcontainers.PostgreSql;

namespace ObfusCal.Tests.Helpers;

public sealed class CustomWebApplicationFactory(string environmentName) : WebApplicationFactory<Program>
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
                ["ConnectionStrings:DefaultConnection"] = Postgres.GetConnectionString()
            }));

        builder.ConfigureServices(services =>
        {
            var storeDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IShadowSlotStore));
            if (storeDescriptor is not null)
                services.Remove(storeDescriptor);

            services.AddSingleton<IShadowSlotStore, InMemoryShadowSlotStore>();
        });
    }
}
