using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Core.Interfaces;
using ObfusCal.Infrastructure.Storage;
using Testcontainers.PostgreSql;

namespace ObfusCal.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _environmentName;
    private readonly PostgreSqlContainer _postgres;

    public CustomWebApplicationFactory(string environmentName)
    {
        _environmentName = environmentName;

        _postgres = new PostgreSqlBuilder("postgres:17")
            .Build();

        // Block here so the container is ready before ConfigureWebHost runs.
        // .GetAwaiter().GetResult() is acceptable in test setup constructors.
        _postgres.StartAsync().GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);

        // Hand the real container connection string to the app so
        // MigrateAsync() in Program.cs can actually connect and succeed.
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString()
            }));

        builder.ConfigureServices(services =>
        {
            // Controller tests don't need the real EF store — keep the fast
            // in-memory replacement so tests stay isolated from each other.
            var storeDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IShadowSlotStore));
            if (storeDescriptor is not null)
                services.Remove(storeDescriptor);

            services.AddSingleton<IShadowSlotStore, InMemoryShadowSlotStore>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();

        base.Dispose(disposing);
    }
}
