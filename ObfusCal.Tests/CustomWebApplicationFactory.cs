using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Core.Interfaces;
using ObfusCal.Infrastructure.Storage;

namespace ObfusCal.Tests;

public sealed class CustomWebApplicationFactory(string environmentName) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);

        // Provide a dummy connection string so Program.cs does not throw;
        // the real database is never reached because IShadowSlotStore is replaced below.
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=testdb;Username=postgres;Password=postgres"
            }));

        builder.ConfigureServices(services =>
        {
            // Replace EfCoreShadowSlotStore with the in-memory implementation for tests.
            var storeDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IShadowSlotStore));
            if (storeDescriptor is not null)
                services.Remove(storeDescriptor);

            services.AddSingleton<IShadowSlotStore, InMemoryShadowSlotStore>();
        });
    }
}
