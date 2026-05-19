using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObfusCal.Domain.Obfuscation;

namespace ObfusCal.Application;

internal static class PluginDiscovery
{
    internal static IReadOnlyList<Type> DiscoverEventTransformerPlugins(ILogger? logger = null) =>
        DiscoverPluginTypes<IObfuscationTransformerPlugin>(logger);

    internal static IReadOnlyList<Type> DiscoverBusySlotTransformerPlugins(ILogger? logger = null) =>
        DiscoverPluginTypes<IBusySlotTransformerPlugin>(logger);

    private static IReadOnlyList<Type> DiscoverPluginTypes<TPlugin>(ILogger? logger = null)
    {
        var pluginInterface = typeof(TPlugin);

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(assembly => GetLoadableTypes(assembly, logger))
            .Where(type => pluginInterface.IsAssignableFrom(type))
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .Distinct()
            .ToList();
    }

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
        catch (BadImageFormatException ex)
        {
            logger?.LogWarning(ex,
                "Could not load types from assembly {AssemblyName}; the assembly will be skipped.",
                assembly.FullName);
            return [];
        }
    }

    internal static void RegisterDiscoveredEventTransformerPlugins(
        this IServiceCollection services,
        ILogger? logger = null)
    {
        foreach (var type in DiscoverEventTransformerPlugins(logger))
        {
            logger?.LogDebug("Registering event transformer plugin {TypeName}", type.FullName);
            services.AddTransient(typeof(IObfuscationTransformer), type);
        }
    }

    internal static void RegisterDiscoveredBusySlotTransformerPlugins(
        this IServiceCollection services,
        ILogger? logger = null)
    {
        foreach (var type in DiscoverBusySlotTransformerPlugins(logger))
        {
            logger?.LogDebug("Registering busy-slot transformer plugin {TypeName}", type.FullName);
            services.AddTransient(typeof(IBusySlotTransformer), type);
        }
    }
}
