using System.Runtime.Loader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Storage;
using ObfusCal.Infrastructure.Sync;
using Serilog;

namespace ObfusCal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                config.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."));

            // Suppress the pending model changes warning — migrations are applied at startup
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.Configure<GraphConsentOptions>(config.GetSection(GraphConsentOptions.SectionName));
        services.Configure<SyncOptions>(config.GetSection(SyncOptions.SectionName));
        services.AddDataProtection();
        services.AddHttpClient<IGraphOAuthTokenClient, GraphOAuthTokenClient>();
        services.AddHttpClient<IcalFeedCalendarSource>();
        services.AddHttpClient(nameof(OutboundPeerSyncService));

        services.AddScoped<ICalendarOwnerScopeResolver, EfCoreCalendarOwnerScopeResolver>();
        services.AddScoped<ICalendarOwnerGraphConsentService, CalendarOwnerGraphConsentService>();
        services.AddScoped<ICalendarOwnerIcalFeedService, CalendarOwnerIcalFeedService>();
        services.AddScoped<IOutboundPeerSyncService, OutboundPeerSyncService>();
        services.AddScoped<IShadowSlotStore, EfCoreShadowSlotStore>();
        services.AddScoped<MockCalendarSource>();
        services.AddScoped<ICalendarSource, IcalFeedCalendarSource>();
        services.AddHostedService<PeerSyncBackgroundService>();

        // Load calendar source plugins from the plugins/ directory alongside the executable.
        // Plugin registrations come after defaults so custom providers can override defaults.
        var pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");

        if (Directory.Exists(pluginFolder))
        {
            foreach (var dll in Directory.GetFiles(pluginFolder, "*.dll"))
            {
                try
                {
                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);

                    var calendarSources = assembly.GetTypes()
                        .Where(t => typeof(ICalendarSource).IsAssignableFrom(t)
                                    && t is { IsInterface: false, IsAbstract: false });

                    foreach (var type in calendarSources)
                    {
                        services.AddScoped(typeof(ICalendarSource), type);
                        Log.ForContext("CalendarSourceType", type.Name)
                            .ForContext("PluginAssemblyPath", dll)
                            .Information("Loaded calendar source plugin");
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext("PluginAssemblyPath", dll)
                        .Error(ex, "Failed to load plugin assembly");
                }
            }
        }


        return services;
    }

    /// <summary>
    /// Applies any pending EF Core migrations. Call from the application composition root
    /// after the DI container is built so that <see cref="AppDbContext"/> can be resolved.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}

