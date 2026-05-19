using System.Reflection;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class CalendarSourcePluginCatalog(
    IReadOnlyList<CalendarSourcePluginDescriptor> plugins,
    PluginAllowlistCache? allowlistCache = null) : ICalendarSourceCatalog
{
    private readonly CalendarSourcePluginDescriptor[] _plugins = plugins
        .OrderBy(plugin => plugin.Id, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<CalendarSourcePluginDescriptor> GetPlugins()
    {
        var blocked = allowlistCache?.GetBlockedPluginIds();
        if (blocked is null or { Count: 0 })
            return _plugins;

        return _plugins.Where(p => !blocked.Contains(p.Id)).ToList();
    }

    public CalendarSourcePluginDescriptor? GetPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return null;

        var plugin = _plugins.SingleOrDefault(plugin =>
            string.Equals(plugin.Id, pluginId.Trim(), StringComparison.OrdinalIgnoreCase));

        if (plugin is null)
            return null;

        var blocked = allowlistCache?.GetBlockedPluginIds();
        return blocked is not null && blocked.Contains(plugin.Id) ? null : plugin;
    }

    internal static IReadOnlyList<CalendarSourcePluginDescriptor> Discover(
        bool includeExternalPlugins,
        PluginAllowlistOptions? allowlist = null,
        ILogger? logger = null)
    {
        var allowedIds = BuildAllowedPluginIds(allowlist);
        var allowedTokens = BuildAllowedPublicKeyTokens(allowlist);
        var pluginsRoot = BuildPluginsRootPath();
        var descriptors = new List<CalendarSourcePluginDescriptor>();

        foreach (var assembly in GetCandidateAssemblies())
        {
            var assemblyPath = GetAssemblyPath(assembly);
            var isExternalAssembly = IsExternalAssembly(includeExternalPlugins, pluginsRoot, assemblyPath);

            if (!IsAssemblyAllowedByToken(assembly, assemblyPath, isExternalAssembly, allowedTokens, logger))
                continue;

            descriptors.AddRange(DiscoverDescriptorsFromAssembly(
                assembly,
                assemblyPath,
                isExternalAssembly,
                allowedIds,
                logger));
        }

        return DeduplicateAndSort(descriptors);
    }

    private static IReadOnlyCollection<Assembly> GetCandidateAssemblies() =>
        AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic).ToArray();

    private static string BuildPluginsRootPath() =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "plugins"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;

    private static HashSet<string>? BuildAllowedPluginIds(PluginAllowlistOptions? allowlist)
    {
        if (allowlist?.Enabled != true || allowlist.AllowedPluginIds.Count == 0)
            return null;

        return new HashSet<string>(
            allowlist.AllowedPluginIds.Select(id => id.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string>? BuildAllowedPublicKeyTokens(PluginAllowlistOptions? allowlist)
    {
        if (allowlist?.AllowedPublicKeyTokens is not { Count: > 0 } tokens)
            return null;

        return new HashSet<string>(
            tokens.Select(t => t.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string GetAssemblyPath(Assembly assembly) =>
        string.IsNullOrWhiteSpace(assembly.Location)
            ? string.Empty
            : Path.GetFullPath(assembly.Location);

    private static bool IsExternalAssembly(bool includeExternalPlugins, string pluginsRoot, string assemblyPath) =>
        includeExternalPlugins
        && !string.IsNullOrWhiteSpace(assemblyPath)
        && assemblyPath.StartsWith(pluginsRoot, StringComparison.OrdinalIgnoreCase);

    private static bool IsAssemblyAllowedByToken(
        Assembly assembly,
        string assemblyPath,
        bool isExternalAssembly,
        HashSet<string>? allowedTokens,
        ILogger? logger)
    {
        if (!isExternalAssembly || allowedTokens is not { Count: > 0 })
            return true;

        var pkt = assembly.GetName().GetPublicKeyToken();
        var pktHex = pkt is { Length: > 0 }
            ? Convert.ToHexString(pkt).ToLowerInvariant()
            : string.Empty;

        if (allowedTokens.Contains(pktHex))
            return true;

        logger?.LogWarning(
            "Rejected external plugin assembly {AssemblyPath}: public key token '{PublicKeyToken}' is not on the allowed list.",
            assemblyPath, pktHex);
        return false;
    }

    private static IReadOnlyList<CalendarSourcePluginDescriptor> DiscoverDescriptorsFromAssembly(
        Assembly assembly,
        string assemblyPath,
        bool isExternalAssembly,
        HashSet<string>? allowedIds,
        ILogger? logger)
    {
        var descriptors = new List<CalendarSourcePluginDescriptor>();

        foreach (var type in GetLoadableTypes(assembly, logger)
                     .Where(type => typeof(ICalendarSource).IsAssignableFrom(type))
                     .Where(type => type is { IsInterface: false, IsAbstract: false }))
        {
            var descriptor = TryBuildDescriptor(type, assemblyPath, isExternalAssembly, allowedIds, logger);
            if (descriptor is not null)
                descriptors.Add(descriptor);
        }

        return descriptors;
    }

    private static CalendarSourcePluginDescriptor? TryBuildDescriptor(
        Type type,
        string assemblyPath,
        bool isExternalAssembly,
        HashSet<string>? allowedIds,
        ILogger? logger)
    {
        var attribute = type.GetCustomAttribute<CalendarSourcePluginAttribute>();
        if (attribute is null)
            return null;

        var pluginId = attribute.Id.Trim().ToLowerInvariant();
        if (isExternalAssembly && allowedIds is not null && !allowedIds.Contains(pluginId))
        {
            logger?.LogWarning(
                "Rejected external plugin '{PluginId}' from assembly {AssemblyPath}: plugin ID is not on the startup allowlist. " +
                "Add it to PluginAllowlist:AllowedPluginIds in appsettings to permit it.",
                pluginId, assemblyPath);
            return null;
        }

        var uiAttribute = type.GetCustomAttribute<CalendarSourcePluginUiAttribute>();
        var actions = type.GetCustomAttributes<CalendarSourcePluginActionAttribute>()
            .Select(a => new CalendarSourcePluginActionDescriptor(a.ActionId, a.Label, a.Hint))
            .ToList();
        var ui = new CalendarSourcePluginUiDescriptor(
            uiAttribute?.SupportsMultipleInstances ?? true,
            uiAttribute?.ConfigurationJsonTemplate,
            uiAttribute?.SecretDataJsonTemplate,
            uiAttribute?.SetupHint,
            actions);

        return new CalendarSourcePluginDescriptor(
            pluginId,
            attribute.DisplayName,
            type,
            isExternalAssembly,
            ui);
    }

    private static IReadOnlyList<CalendarSourcePluginDescriptor> DeduplicateAndSort(
        IEnumerable<CalendarSourcePluginDescriptor> descriptors) =>
        descriptors
            .GroupBy(descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly, ILogger? logger = null)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            logger?.LogWarning(ex,
                "Partial type load failure for assembly {AssemblyName}; some types may be unavailable.",
                assembly.FullName);
            return ex.Types.Where(type => type is not null)!;
        }
        catch (FileLoadException ex)
        {
            logger?.LogWarning(ex,
                "Could not load types from assembly {AssemblyName}; the assembly will be skipped.",
                assembly.FullName);
            return [];
        }
        catch (FileNotFoundException ex)
        {
            logger?.LogWarning(ex,
                "Could not load types from assembly {AssemblyName}; the assembly will be skipped.",
                assembly.FullName);
            return [];
        }
        catch (BadImageFormatException ex)
        {
            logger?.LogWarning(ex,
                "Could not load types from assembly {AssemblyName}; the assembly will be skipped.",
                assembly.FullName);
            return [];
        }
        catch (NotSupportedException ex)
        {
            logger?.LogWarning(ex,
                "Could not load types from assembly {AssemblyName}; the assembly will be skipped.",
                assembly.FullName);
            return [];
        }
    }
}

