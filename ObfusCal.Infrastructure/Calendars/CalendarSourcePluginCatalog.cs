using System.Reflection;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class CalendarSourcePluginCatalog(IReadOnlyList<CalendarSourcePluginDescriptor> plugins) : ICalendarSourceCatalog
{
    private readonly CalendarSourcePluginDescriptor[] _plugins = plugins
        .OrderBy(plugin => plugin.Id, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<CalendarSourcePluginDescriptor> GetPlugins() => _plugins;

    public CalendarSourcePluginDescriptor? GetPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return null;

        return _plugins.SingleOrDefault(plugin =>
            string.Equals(plugin.Id, pluginId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    internal static IReadOnlyList<CalendarSourcePluginDescriptor> Discover(bool includeExternalPlugins)
    {
        var descriptors = new List<CalendarSourcePluginDescriptor>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic))
        {
            var isExternalAssembly = includeExternalPlugins
                && !string.IsNullOrWhiteSpace(assembly.Location)
                && assembly.Location.StartsWith(Path.Combine(AppContext.BaseDirectory, "plugins"), StringComparison.OrdinalIgnoreCase);

            foreach (var type in GetLoadableTypes(assembly)
                         .Where(type => typeof(ICalendarSource).IsAssignableFrom(type))
                         .Where(type => type is { IsInterface: false, IsAbstract: false }))
            {
                var attribute = type.GetCustomAttribute<CalendarSourcePluginAttribute>();
                if (attribute is null)
                    continue;

                descriptors.Add(new CalendarSourcePluginDescriptor(
                    attribute.Id.Trim().ToLowerInvariant(),
                    attribute.DisplayName,
                    type,
                    isExternalAssembly));
            }
        }

        return descriptors
            .GroupBy(descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
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
}


