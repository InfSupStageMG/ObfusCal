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
using ObfusCal.Infrastructure.Security;
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
        LoadPluginAssemblies();

        RegisterCoreInfrastructure(services, config);
        RegisterHttpClients(services);
        RegisterDomainServices(services);
        RegisterCalendarSourcePlugins(services);
        RegisterCalendarSourceServices(services);

        return services;
    }

    private static void RegisterCoreInfrastructure(IServiceCollection services, IConfiguration config)
    {
        services.Configure<SecretProviderOptions>(config.GetSection(SecretProviderOptions.SectionName));
        services.AddSingleton<EnvironmentSecretProvider>();
        services.AddSingleton<ExternalSecretProvider>();
        services.AddSingleton<ISecretProvider, ConfiguredSecretProvider>();
        services.AddSingleton<ILogRedactor, DefaultLogRedactor>();
        services.AddSingleton<ISyncRuntimeOptionsProvider, SyncRuntimeOptionsProvider>();
        services.AddSingleton<SecretStartupValidator>();

        services.Configure<SecretValidationOptions>(options =>
        {
            options.RequiredSecretKeys.Add(SecretKeys.DefaultConnectionString);
            options.RequiredSecretKeys.Add(SecretKeys.AzureAdTenantId);
            options.RequiredSecretKeys.Add(SecretKeys.AzureAdClientId);
            options.RequiredSecretKeys.Add(SecretKeys.GraphConsentClientId);
        });

        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var secretProvider = provider.GetRequiredService<ISecretProvider>();
            var connectionString = secretProvider.GetSecret(SecretKeys.DefaultConnectionString)
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            options.UseNpgsql(
                connectionString);

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
        services.AddScoped<ICalendarOwnerCalendarSourceService, CalendarOwnerCalendarSourceService>();
        services.AddScoped<ICalendarOwnerIcalFeedService, CalendarOwnerIcalFeedService>();
        services.AddScoped<ICalendarOwnerObfuscationProfileService, CalendarOwnerObfuscationProfileService>();
        services.AddScoped<ICalendarOwnerClientBusySlotService, CalendarOwnerClientBusySlotService>();
        services.AddScoped<ICalendarOwnerAvailabilitySyncService, CalendarOwnerAvailabilitySyncService>();
        services.AddScoped<IPeerConnectionService, PeerConnectionService>();
        services.AddScoped<IStatusService, StatusService>();
        services.AddScoped<IOutboundPeerSyncService, OutboundPeerSyncService>();
        services.AddScoped<IInboundPeerPullSyncService, InboundPeerPullSyncService>();
        services.AddScoped<IShadowSlotStore, EfCoreShadowSlotStore>();
        services.AddScoped<ICalendarOwnerAvailabilitySlotStore, EfCoreCalendarOwnerAvailabilitySlotStore>();
        services.AddScoped<MockCalendarSource>();
        services.AddScoped<IcalFeedCalendarSource>();
        services.AddHostedService<CalendarOwnerAvailabilityBackgroundService>();
        services.AddHostedService<PeerSyncBackgroundService>();
    }

    private static void RegisterCalendarSourcePlugins(IServiceCollection services)
    {
        var catalog = new CalendarSourcePluginCatalog(CalendarSourcePluginCatalog.Discover(includeExternalPlugins: true));
        services.AddSingleton<ICalendarSourceCatalog>(catalog);

        foreach (var plugin in catalog.GetPlugins())
        {
            if (services.Any(descriptor => descriptor.ServiceType == plugin.ImplementationType))
                continue;

            services.AddScoped(plugin.ImplementationType);

            Log.ForContext("CalendarSourceType", plugin.ImplementationType.Name)
                .ForContext("CalendarSourceId", plugin.Id)
                .ForContext("PluginAssemblyPath", plugin.IsExternalPlugin ? plugin.ImplementationType.Assembly.Location : "built-in")
                .Information("Registered calendar source plugin");
        }
    }

    private static void RegisterCalendarSourceServices(IServiceCollection services)
    {
        services.AddScoped<ICalendarSourceResolver, CalendarSourceResolver>();
        services.AddScoped<ICalendarSource>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<CalendarSourceOptions>>().Value;
            var environment = provider.GetRequiredService<IHostEnvironment>();
            var catalog = provider.GetRequiredService<ICalendarSourceCatalog>();
            var configuredProviderId = ResolveConfiguredCalendarProvider(options.Provider, environment);
            var plugin = catalog.GetPlugin(configuredProviderId);

            plugin ??= catalog.GetPlugins().FirstOrDefault()
                ?? throw new InvalidOperationException("No calendar source plugins are registered.");

            return (ICalendarSource)provider.GetRequiredService(plugin.ImplementationType);
        });
    }

    private static void LoadPluginAssemblies()
    {
        // Load plugin assemblies from the plugins/ directory alongside the executable.
        var pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginFolder))
            return;

        foreach (var dll in Directory.GetFiles(pluginFolder, "*.dll"))
        {
            LoadPluginAssembly(dll);
        }
    }

    private static void LoadPluginAssembly(string dll)
    {
        try
        {
            AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);

            Log.ForContext("PluginAssemblyPath", dll)
                .Information("Loaded plugin assembly");
        }
        catch (Exception ex)
        {
            Log.ForContext("PluginAssemblyPath", dll)
                .Error(ex, "Failed to load plugin assembly");
        }
    }

    private static string ResolveConfiguredCalendarProvider(string? configuredProvider, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider))
            configuredProvider = environment.IsDevelopment() ? "mock" : "graph";

        return configuredProvider.Trim().ToLowerInvariant();
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

