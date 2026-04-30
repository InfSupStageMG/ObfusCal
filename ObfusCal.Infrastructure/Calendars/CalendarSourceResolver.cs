using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class CalendarSourceResolver(
    AppDbContext dbContext,
    ICalendarSourceCatalog catalog,
    IServiceProvider serviceProvider,
    IOptions<CalendarSourceOptions> options,
    IHostEnvironment environment)
    : ICalendarSourceResolver
{
    public async Task<ICalendarSource> ResolveAsync(Guid? calendarOwnerId = null, CancellationToken ct = default)
    {
        var pluginId = await ResolvePluginIdAsync(calendarOwnerId, ct);
        var plugin = catalog.GetPlugin(pluginId)
            ?? throw new InvalidOperationException($"Calendar source plugin '{pluginId}' is not registered.");

        return (ICalendarSource)serviceProvider.GetRequiredService(plugin.ImplementationType);
    }

    private async Task<string> ResolvePluginIdAsync(Guid? calendarOwnerId, CancellationToken ct)
    {
        if (calendarOwnerId is { } ownerId)
        {
            var ownerPluginId = await dbContext.CalendarOwners
                .AsNoTracking()
                .Where(owner => owner.Id == ownerId)
                .Select(owner => owner.CalendarSourcePluginId)
                .SingleOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(ownerPluginId) && catalog.GetPlugin(ownerPluginId) is not null)
                return ownerPluginId.Trim().ToLowerInvariant();
        }

        var defaultPluginId = environment.IsDevelopment() ? "mock" : "graph";
        var configuredPluginId = !string.IsNullOrWhiteSpace(options.Value.Provider)
            ? options.Value.Provider.Trim().ToLowerInvariant()
            : defaultPluginId;
        if (!string.IsNullOrWhiteSpace(configuredPluginId) && catalog.GetPlugin(configuredPluginId) is not null)
            return configuredPluginId;

        return catalog.GetPlugins().FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("No calendar source plugins are registered.");
    }
}


