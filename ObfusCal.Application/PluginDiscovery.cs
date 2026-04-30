using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Domain.Obfuscation;

namespace ObfusCal.Application;

internal static class PluginDiscovery
{
    internal static IReadOnlyList<Type> DiscoverEventTransformerPlugins() =>
        DiscoverPluginTypes<IObfuscationTransformerPlugin>();

    internal static IReadOnlyList<Type> DiscoverBusySlotTransformerPlugins() =>
        DiscoverPluginTypes<IBusySlotTransformerPlugin>();

    private static IReadOnlyList<Type> DiscoverPluginTypes<TPlugin>()
    {
        var pluginInterface = typeof(TPlugin);

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(GetLoadableTypes)
            .Where(type => pluginInterface.IsAssignableFrom(type))
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .Distinct()
            .ToList();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }

    internal static void RegisterDiscoveredEventTransformerPlugins(this IServiceCollection services)
    {
        foreach (var type in DiscoverEventTransformerPlugins())
            services.AddTransient(typeof(IObfuscationTransformer), type);
    }

    internal static void RegisterDiscoveredBusySlotTransformerPlugins(this IServiceCollection services)
    {
        foreach (var type in DiscoverBusySlotTransformerPlugins())
            services.AddTransient(typeof(IBusySlotTransformer), type);
    }
}

