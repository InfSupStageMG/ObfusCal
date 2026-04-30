using System.Runtime.Loader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        RegisterCoreInfrastructure(services, config);
        RegisterHttpClients(services);
        RegisterDomainServices(services);
        RegisterCalendarSourceResolver(services);
        RegisterCalendarSourcePlugins(services);

        return services;
    }

    private static void RegisterCoreInfrastructure(IServiceCollection services, IConfiguration config)
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
        services.Configure<CalendarSourceOptions>(config.GetSection(CalendarSourceOptions.SectionName));
        services.Configure<SyncOptions>(config.GetSection(SyncOptions.SectionName));
        services.AddDataProtection();
    }

    private static void RegisterHttpClients(IServiceCollection services)
    {
        services.AddHttpClient<IGraphOAuthTokenClient, GraphOAuthTokenClient>();
        services.AddHttpClient<GraphCalendarSource>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<GraphConsentOptions>>().Value;
            var baseUrl = options.ApiBaseUrl.Trim();

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("GraphConsent:ApiBaseUrl is required.");

            client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/", UriKind.Absolute);
        });
        services.AddHttpClient<IcalFeedCalendarSource>();
        services.AddHttpClient(nameof(OutboundPeerSyncService));
        services.AddHttpClient(nameof(InboundPeerPullSyncService));
    }

    private static void RegisterDomainServices(IServiceCollection services)
    {
        services.AddScoped<ICalendarOwnerScopeResolver, EfCoreCalendarOwnerScopeResolver>();
        services.AddScoped<ICalendarOwnerService, CalendarOwnerService>();
        services.AddScoped<ICalendarOwnerGraphConsentService, CalendarOwnerGraphConsentService>();
        services.AddScoped<ICalendarOwnerIcalFeedService, CalendarOwnerIcalFeedService>();
        services.AddScoped<ICalendarOwnerObfuscationProfileService, CalendarOwnerObfuscationProfileService>();
        services.AddScoped<ICalendarOwnerAvailabilitySyncService, CalendarOwnerAvailabilitySyncService>();
        services.AddScoped<IPeerConnectionService, PeerConnectionService>();
        services.AddScoped<IOutboundPeerSyncService, OutboundPeerSyncService>();
        services.AddScoped<IInboundPeerPullSyncService, InboundPeerPullSyncService>();
        services.AddScoped<IShadowSlotStore, EfCoreShadowSlotStore>();
        services.AddScoped<MockCalendarSource>();
        services.AddScoped<IcalFeedCalendarSource>();
        services.AddHostedService<CalendarOwnerAvailabilityBackgroundService>();
        services.AddHostedService<PeerSyncBackgroundService>();
    }

    private static void RegisterCalendarSourceResolver(IServiceCollection services)
    {
        services.AddScoped(provider =>
        {
            var options = provider.GetRequiredService<IOptions<CalendarSourceOptions>>().Value;
            var environment = provider.GetRequiredService<IHostEnvironment>();
            var configuredProvider = ResolveConfiguredCalendarProvider(options.Provider, environment);

            return configuredProvider switch
            {
                "mock" => (ICalendarSource)provider.GetRequiredService<MockCalendarSource>(),
                "ical" => provider.GetRequiredService<IcalFeedCalendarSource>(),
                _ => provider.GetRequiredService<GraphCalendarSource>()
            };
        });
    }

    private static string ResolveConfiguredCalendarProvider(string? configuredProvider, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider))
            configuredProvider = environment.IsDevelopment() ? "Mock" : "Graph";

        return configuredProvider.Trim().ToLowerInvariant();
    }

    private static void RegisterCalendarSourcePlugins(IServiceCollection services)
    {
        // Load calendar source plugins from the plugins/ directory alongside the executable.
        // Plugin registrations come after defaults so custom providers can override defaults.
        var pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginFolder))
            return;

        foreach (var dll in Directory.GetFiles(pluginFolder, "*.dll"))
        {
            RegisterCalendarSourcePluginAssembly(services, dll);
        }
    }

    private static void RegisterCalendarSourcePluginAssembly(IServiceCollection services, string dll)
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

