using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
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
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ObfusCal.Infrastructure;

public static class DependencyInjection
{
    private static readonly string[] RequiredDefaultPluginIds = ["graph", "ical", "mock", "google", "icloud"];
    private static readonly string[] OfficialPluginAssemblyNames = [
        "ObfusCal.Plugins.GoogleCalendar",
        "ObfusCal.Plugins.ICloudCalendar"
    ];

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        LoadPluginAssemblies();

        RegisterCoreInfrastructure(services, config);
        RegisterHttpClients(services);
        RegisterDomainServices(services);
        RegisterCalendarSourcePlugins(services, config);
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
        services.AddSingleton<IColumnEncryptor>(provider =>
            new AesGcmColumnEncryptor(provider.GetRequiredService<ISecretProvider>()));
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
            options.RequiredSecretKeys.Add(SecretKeys.ColumnEncryptionKey);
        });

        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var secretProvider = provider.GetRequiredService<ISecretProvider>();
            var connectionString = secretProvider.GetSecret(SecretKeys.DefaultConnectionString)
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            options.UseNpgsql(
                connectionString);

            // Suppress the pending model changes warning - migrations are applied at startup
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.Configure<GraphConsentOptions>(config.GetSection(GraphConsentOptions.SectionName));
        services.Configure<GoogleConsentOptions>(config.GetSection(GoogleConsentOptions.SectionName));
        services.Configure<CalendarSourceOptions>(config.GetSection(CalendarSourceOptions.SectionName));
        services.Configure<ICloudCalendarOptions>(config.GetSection(ICloudCalendarOptions.SectionName));
        services.Configure<SyncOptions>(config.GetSection(SyncOptions.SectionName));
        services.Configure<PeerTransportSecurityOptions>(config.GetSection(PeerTransportSecurityOptions.SectionName));
        services.Configure<PluginAllowlistOptions>(config.GetSection(PluginAllowlistOptions.SectionName));

        // Configure DataProtection with persistent key storage for credential encryption
        // Keys are stored in /dataprotection/keys (must be mounted as a persistent volume in containers)
        // See: docs/07-deployment-view.md and Dockerfile for volume configuration
        ConfigureDataProtection(services);
    }

    private static void ConfigureDataProtection(IServiceCollection services)
    {
        // Keys are stored in the DataProtectionKeys table in PostgreSQL so they survive container
        // rebuilds and do not depend on a separately mounted filesystem volume.
        // The migration that creates the table runs at startup via MigrateDatabaseAsync().
        services.AddDataProtection()
            .SetApplicationName("ObfusCal")
            .PersistKeysToDbContext<AppDbContext>();

        Log.Information(
            "DataProtection keys will be persisted to PostgreSQL via AppDbContext. " +
            "Ensure the database is available and migrations have run before the first OIDC request.");
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

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15)
        };

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
        services.AddHostedService<ShadowSlotRetentionBackgroundService>();
    }

    private static void RegisterCalendarSourcePlugins(IServiceCollection services, IConfiguration config)
    {
        var allowlist = config.GetSection(PluginAllowlistOptions.SectionName).Get<PluginAllowlistOptions>()
                        ?? new PluginAllowlistOptions();
        EnsureDefaultPluginIds(allowlist);

        var cache = new PluginAllowlistCache();
        services.AddSingleton(cache);
        services.AddScoped<IPluginAllowlistAdminService, EfCorePluginAllowlistAdminService>();

        var discovered = CalendarSourcePluginCatalog.Discover(
            includeExternalPlugins: true,
            allowlist: allowlist,
            logger: Log.Logger.ForContext<CalendarSourcePluginCatalog>() as ILogger);
        EnsureRequiredPluginsDiscovered(discovered);

        var catalog = new CalendarSourcePluginCatalog(discovered, cache);
        services.AddSingleton<ICalendarSourceCatalog>(catalog);

        foreach (var plugin in discovered)
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
        // Prefer dependency-context loading for official plugins, then fall back to plugins/ folder probing.
        LoadOfficialPluginsFromDependencyContext();

        foreach (var pluginFolder in GetPluginFolders())
        {
            var dllFiles = Directory.GetFiles(pluginFolder, "*.dll");
            if (dllFiles.Length == 0)
                continue;

            Log.ForContext("PluginFolder", pluginFolder)
                .ForContext("DllCount", dllFiles.Length)
                .Information("Loading plugin assemblies from folder");

            foreach (var dll in dllFiles)
                LoadPluginAssembly(dll);
        }

        EnsureOfficialPluginAssembliesLoaded();
    }

    private static IEnumerable<string> GetPluginFolders()
    {
        var candidateFolders = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "plugins")
        };

        foreach (var folder in candidateFolders
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(folder))
                yield return folder;
        }
    }

    private static void LoadOfficialPluginsFromDependencyContext()
    {
        foreach (var assemblyName in OfficialPluginAssemblyNames)
        {
            if (IsAssemblyLoaded(assemblyName))
                continue;

            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
                Log.ForContext("AssemblyName", assemblyName)
                    .Information("Loaded official plugin assembly via dependency context");
            }
            catch (Exception ex)
            {
                Log.ForContext("AssemblyName", assemblyName)
                    .Debug(ex, "Official plugin assembly not resolved from dependency context; folder probing will continue");
            }
        }
    }

    private static void EnsureOfficialPluginAssembliesLoaded()
    {
        foreach (var assemblyName in OfficialPluginAssemblyNames)
        {
            if (IsAssemblyLoaded(assemblyName))
                continue;

            throw new InvalidOperationException(
                $"Official plugin assembly '{assemblyName}' is not loaded. " +
                "Google and iCloud plugins must be available at startup.");
        }
    }

    private static void LoadPluginAssembly(string dll)
    {
        try
        {
            var assemblyName = Path.GetFileNameWithoutExtension(dll);
            if (IsAssemblyLoaded(assemblyName))
                return;

            AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);

            Log.ForContext("AssemblyName", assemblyName)
                .ForContext("PluginAssemblyPath", dll)
                .Information("Successfully loaded plugin assembly");
        }
        catch (Exception ex)
        {
            Log.ForContext("PluginAssemblyPath", dll)
                .Error(ex, "Failed to load plugin assembly");
        }
    }

    private static bool IsAssemblyLoaded(string assemblyName) =>
        AppDomain.CurrentDomain.GetAssemblies().Any(a =>
            string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

    private static void EnsureDefaultPluginIds(PluginAllowlistOptions allowlist)
    {
        if (!allowlist.Enabled)
            return;

        foreach (var requiredPluginId in RequiredDefaultPluginIds)
        {
            if (allowlist.AllowedPluginIds.Any(id =>
                    string.Equals(id?.Trim(), requiredPluginId, StringComparison.OrdinalIgnoreCase)))
                continue;

            allowlist.AllowedPluginIds.Add(requiredPluginId);
        }
    }

    private static void EnsureRequiredPluginsDiscovered(IReadOnlyList<CalendarSourcePluginDescriptor> discovered)
    {
        var discoveredIds = discovered.Select(plugin => plugin.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = RequiredDefaultPluginIds
            .Where(requiredId => !discoveredIds.Contains(requiredId))
            .ToArray();

        if (missing.Length == 0)
            return;

        throw new InvalidOperationException(
            $"Required plugins are missing from startup registration: {string.Join(", ", missing)}.");
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

    public static async Task InitializePluginAllowlistAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cache = services.GetRequiredService<PluginAllowlistCache>();

        var blockedIds = await db.PluginAllowlistOverrides
            .Where(o => !o.IsEnabled)
            .Select(o => o.PluginId)
            .ToListAsync();

        cache.Initialize(blockedIds);
    }
}

