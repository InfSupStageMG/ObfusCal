using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
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
        services.AddSingleton<ICalendarSourceSecretProtector, CalendarSourceSecretProtector>();
        services.AddSingleton<ISyncRuntimeOptionsProvider, SyncRuntimeOptionsProvider>();
        services.AddSingleton<IUrlSafetyValidator, UrlSafetyValidator>();
        services.AddSingleton<SecretStartupValidator>();

        services.Configure<SecretValidationOptions>(options =>
        {
            options.RequiredSecretKeys.Add(SecretKeys.DefaultConnectionString);
            options.RequiredSecretKeys.Add(SecretKeys.AzureAdTenantId);
            options.RequiredSecretKeys.Add(SecretKeys.AzureAdClientId);
            options.RequiredSecretKeys.Add(SecretKeys.AzureAdClientSecret);
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
        services.Configure<GoogleConsentOptions>(config.GetSection(GoogleConsentOptions.SectionName));
        services.Configure<CalendarSourceOptions>(config.GetSection(CalendarSourceOptions.SectionName));
        services.Configure<ICloudCalendarOptions>(config.GetSection(ICloudCalendarOptions.SectionName));
        services.Configure<SyncOptions>(config.GetSection(SyncOptions.SectionName));
        services.Configure<PeerTransportSecurityOptions>(config.GetSection(PeerTransportSecurityOptions.SectionName));

        // Configure DataProtection with persistent key storage for credential encryption
        // Keys are stored in /dataprotection/keys (must be mounted as a persistent volume in containers)
        // See: docs/07-deployment-view.md and Dockerfile for volume configuration
        ConfigureDataProtection(services);
    }

    private static void ConfigureDataProtection(IServiceCollection services)
    {
        var dataProtectionKeyPath = Environment.GetEnvironmentVariable("DATAPROTECTION_KEYS_PATH")
            ?? "/dataprotection/keys";

        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName("ObfusCal");

        // Attempt to use persistent key storage if the directory exists or can be created
        try
        {
            if (!Path.IsPathRooted(dataProtectionKeyPath))
            {
                dataProtectionKeyPath = Path.Combine(AppContext.BaseDirectory, dataProtectionKeyPath);
            }

            Directory.CreateDirectory(dataProtectionKeyPath);
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));

            Log.ForContext("DataProtectionKeyPath", dataProtectionKeyPath)
                .Information("DataProtection keys will be persisted to {Path}. Ensure this directory is mounted as a persistent volume in containers.", dataProtectionKeyPath);
        }
        catch (Exception ex)
        {
            Log.ForContext("DataProtectionKeyPath", dataProtectionKeyPath)
                .Warning(ex, "Failed to configure persistent DataProtection key storage. Keys will be ephemeral and will be lost on application restart. Re-saving iCloud configurations may be required after restarts.");
        }
    }

    private static void RegisterHttpClients(IServiceCollection services)
    {
        services.AddHttpClient<IGraphOAuthTokenClient, GraphOAuthTokenClient>();
        services.AddHttpClient<IGoogleOAuthTokenClient, GoogleOAuthTokenClient>();
        services.AddHttpClient<GraphCalendarSource>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<GraphConsentOptions>>().Value;
            var baseUrl = options.ApiBaseUrl.Trim();

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("GraphConsent:ApiBaseUrl is required.");

            client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/", UriKind.Absolute);
        });
        services.AddHttpClient<IcalFeedCalendarSource>();
        services.AddHttpClient<GoogleCalendarSourceCore>();
        services.AddHttpClient<ICloudCalendarSourceCore>();
        services.AddHttpClient(nameof(OutboundPeerSyncService))
            .ConfigurePrimaryHttpMessageHandler(CreatePeerTransportHandler);
        services.AddHttpClient(nameof(InboundPeerPullSyncService))
            .ConfigurePrimaryHttpMessageHandler(CreatePeerTransportHandler);
    }

    private static HttpMessageHandler CreatePeerTransportHandler(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<PeerTransportSecurityOptions>>().Value;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("PeerTransportHttpClient");

        var handler = new SocketsHttpHandler();
        handler.SslOptions.RemoteCertificateValidationCallback =
            (sender, cert, chain, errors) => ValidatePeerRemoteCertificate(sender, cert as X509Certificate2, chain, errors, options, logger);
        handler.SslOptions.LocalCertificateSelectionCallback =
            (sender, targetHost, localCerts, remoteCert, issuers) =>
            SelectPeerLocalCertificate(sender, targetHost, localCerts, remoteCert, issuers, logger);

        return handler;
    }

    private static bool ValidatePeerRemoteCertificate(
        object sender,
        X509Certificate2? certificate,
        X509Chain? chain,
        System.Net.Security.SslPolicyErrors sslPolicyErrors,
        PeerTransportSecurityOptions options,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        if (certificate == null)
            return false;

        var request = sender as HttpRequestMessage;
        var pinnedThumbprint = request?.Options.TryGetValue(PeerTransportRequestOptions.PinnedCertificateThumbprint, out var pinned) == true
            ? pinned
            : null;

        var result = PeerTransportSecurity.ValidateRemoteCertificate(
            certificate,
            sslPolicyErrors,
            pinnedThumbprint,
            options.AllowSelfSignedCerts);

        if (!result.IsTrusted)
        {
            logger.LogWarning(
                "Rejected peer certificate for {PeerId}: {Reason}",
                request?.Options.TryGetValue(PeerTransportRequestOptions.PeerInstanceId, out var peerId) == true ? peerId : "<unknown>",
                result.FailureReason);
        }

        return result.IsTrusted;
    }

    private static X509Certificate? SelectPeerLocalCertificate(
        object sender,
        string targetHost,
        X509CertificateCollection localCertificates,
        X509Certificate? remoteCertificate,
        string[] acceptableIssuers,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        var request = sender as HttpRequestMessage;
        var clientThumbprint = request?.Options.TryGetValue(PeerTransportRequestOptions.ClientCertificateThumbprint, out var thumbprint) == true
            ? thumbprint
            : null;
        var peerId = request?.Options.TryGetValue(PeerTransportRequestOptions.PeerInstanceId, out var id) == true ? id : null;

        return PeerTransportSecurity.TryResolveClientCertificate(clientThumbprint, logger, peerId);
    }

     private static void RegisterDomainServices(IServiceCollection services)
    {
        services.AddScoped(provider =>
        {
            var options = provider.GetRequiredService<IOptions<GoogleConsentOptions>>();
            var tokenClient = provider.GetRequiredService<IGoogleOAuthTokenClient>();
            return new GoogleOAuthDependencies(options, tokenClient);
        });

        services.AddScoped(provider =>
        {
            var service = provider.GetRequiredService<ICalendarSourceInstanceService>();
            var store = provider.GetRequiredService<ICalendarSourceInstanceStore>();
            return new GoogleConsentInstanceDependencies(service, store);
        });

        services.AddScoped<ICalendarOwnerScopeResolver, EfCoreCalendarOwnerScopeResolver>();
        services.AddScoped<ICalendarOwnerProvisioningService, CalendarOwnerProvisioningService>();
        services.AddScoped<ICalendarOwnerService, CalendarOwnerService>();
        services.AddScoped<ICalendarOwnerGraphConsentService, CalendarOwnerGraphConsentService>();
        services.AddScoped<ICalendarOwnerGoogleConsentService, CalendarOwnerGoogleConsentService>();
        services.AddScoped<ICalendarOwnerCalendarSourceService, CalendarOwnerCalendarSourceService>();
        services.AddScoped<ICalendarSourceInstanceService, CalendarSourceInstanceService>();
        services.AddScoped<ICalendarSourceInstanceStore>(provider =>
            (ICalendarSourceInstanceStore)provider.GetRequiredService<ICalendarSourceInstanceService>());
        services.AddScoped<ICalendarOwnerICloudConfigurationService, CalendarOwnerICloudConfigurationService>();
        services.AddScoped<ICalendarOwnerIcalFeedService, CalendarOwnerIcalFeedService>();
        services.AddScoped<ICalendarOwnerObfuscationProfileService, CalendarOwnerObfuscationProfileService>();
        services.AddScoped<ICalendarOwnerClientBusySlotService, CalendarOwnerClientBusySlotService>();
        services.AddScoped<ICalendarOwnerAvailabilitySyncService, CalendarOwnerAvailabilitySyncService>();
        services.AddScoped<IPeerConnectionService, PeerConnectionService>();
        services.AddScoped<IStatusService, StatusService>();
        services.AddScoped<IOutboundPeerSyncService, OutboundPeerSyncService>();
        services.AddScoped<IInboundPeerPullSyncService, InboundPeerPullSyncService>();
        services.AddScoped<IPeerApiKeyAuthenticator, EfCorePeerApiKeyAuthenticator>();
        services.AddScoped<IShadowSlotStore, EfCoreShadowSlotStore>();
        services.AddScoped<IPeerCalendarOwnerResolver, EfCorePeerCalendarOwnerResolver>();
        services.AddScoped<ICalendarOwnerAvailabilitySlotStore, EfCoreCalendarOwnerAvailabilitySlotStore>();
        services.AddScoped<MockCalendarSource>();
        services.AddScoped<IcalFeedCalendarSource>();
        services.AddScoped<GoogleCalendarSourceCore>();
        services.AddScoped<ICloudCalendarSourceCore>();
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

