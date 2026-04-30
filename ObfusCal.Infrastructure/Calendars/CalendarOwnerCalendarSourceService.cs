using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class CalendarOwnerCalendarSourceService(
    AppDbContext dbContext,
    ICalendarSourceCatalog catalog,
    IServiceProvider serviceProvider,
    IOptions<CalendarSourceOptions> options,
    IHostEnvironment environment)
    : ICalendarOwnerCalendarSourceService
{
    public async Task<IReadOnlyList<CalendarSourceProviderInfo>> ListProvidersAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var selection = await GetSelectionAsync(calendarOwnerId, ct);
        if (selection is null)
            return [];

        var selectedId = selection.Id;
        var providers = new List<CalendarSourceProviderInfo>();

        foreach (var plugin in catalog.GetPlugins())
        {
            var readiness = await GetReadinessAsync(plugin, calendarOwnerId, ct);
            providers.Add(new CalendarSourceProviderInfo(
                plugin.Id,
                plugin.DisplayName,
                string.Equals(plugin.Id, selectedId, StringComparison.OrdinalIgnoreCase),
                readiness.IsReady,
                readiness.Title,
                readiness.Detail,
                plugin.IsExternalPlugin));
        }

        return providers;
    }

    public async Task<CalendarSourceSelection?> GetSelectionAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var owner = await dbContext.CalendarOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(calendarOwner => calendarOwner.Id == calendarOwnerId, ct);

        if (owner is null)
            return null;

        var selectedPlugin = ResolveSelectedPlugin(owner);
        var readiness = await GetReadinessAsync(selectedPlugin, calendarOwnerId, ct);

        return new CalendarSourceSelection(
            owner.Id,
            selectedPlugin.Id,
            selectedPlugin.DisplayName,
            readiness.IsReady,
            readiness.Title,
            readiness.Detail,
            selectedPlugin.IsExternalPlugin);
    }

    public async Task<CalendarSourceSelection?> SetSelectionAsync(Guid calendarOwnerId, string pluginId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new InvalidOperationException("A calendar source plugin id is required.");

        var plugin = catalog.GetPlugin(pluginId)
            ?? throw new InvalidOperationException($"Calendar source plugin '{pluginId}' is not registered.");

        var owner = await dbContext.CalendarOwners
            .SingleOrDefaultAsync(calendarOwner => calendarOwner.Id == calendarOwnerId, ct);

        if (owner is null)
            return null;

        owner.CalendarSourcePluginId = plugin.Id;
        await dbContext.SaveChangesAsync(ct);

        var readiness = await GetReadinessAsync(plugin, calendarOwnerId, ct);
        return new CalendarSourceSelection(
            owner.Id,
            plugin.Id,
            plugin.DisplayName,
            readiness.IsReady,
            readiness.Title,
            readiness.Detail,
            plugin.IsExternalPlugin);
    }

    private CalendarSourcePluginDescriptor ResolveSelectedPlugin(CalendarOwner owner)
    {
        if (!string.IsNullOrWhiteSpace(owner.CalendarSourcePluginId))
        {
            var selectedPlugin = catalog.GetPlugin(owner.CalendarSourcePluginId);
            if (selectedPlugin is not null)
                return selectedPlugin;
        }

        var configuredPlugin = catalog.GetPlugin(ResolveConfiguredProviderId());
        if (configuredPlugin is not null)
            return configuredPlugin;

        return catalog.GetPlugins().FirstOrDefault()
            ?? throw new InvalidOperationException("No calendar source plugins are registered.");
    }

    private string ResolveConfiguredProviderId()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.Provider))
            return options.Value.Provider.Trim().ToLowerInvariant();

        return environment.IsDevelopment() ? "mock" : "graph";
    }

    private async Task<CalendarSourceReadiness> GetReadinessAsync(
        CalendarSourcePluginDescriptor plugin,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        var source = (ICalendarSource)serviceProvider.GetRequiredService(plugin.ImplementationType);
        if (source is not ICalendarSourceReadinessEvaluator evaluator)
            return CalendarSourceReadiness.Ready();

        return await evaluator.GetReadinessAsync(calendarOwnerId, ct);
    }
}



