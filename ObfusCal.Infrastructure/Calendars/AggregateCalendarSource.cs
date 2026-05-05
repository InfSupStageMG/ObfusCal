using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class AggregateCalendarSource(
    Guid ownerId,
    ICalendarSourceCatalog catalog,
    ICalendarSourceInstanceStore instanceStore,
    IServiceProvider serviceProvider,
    ILogger<AggregateCalendarSource> logger)
    : ICalendarSource
{
    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? calendarOwnerId = null,
        CancellationToken ct = default)
    {
        var instances = await instanceStore.ListAsync(ownerId, ct);
        var enabledInstances = instances
            .Where(instance => instance.IsEnabled)
            .Where(instance => catalog.GetPlugin(instance.PluginId) is not null)
            .ToList();

        if (enabledInstances.Count == 0)
            return [];

        var events = new List<CalendarEvent>();

        foreach (var instance in enabledInstances)
        {
            var plugin = catalog.GetPlugin(instance.PluginId);
            if (plugin is null)
                continue;

            try
            {
                var source = (ICalendarSource)serviceProvider.GetRequiredService(plugin.ImplementationType);
                IReadOnlyList<CalendarEvent> instanceEvents;
                if (source is ICalendarSourceInstanceHandler instanceHandler)
                {
                    instanceEvents = await instanceHandler.GetEventsAsync(instance, from, to, ct);
                }
                else
                {
                    instanceEvents = await source.GetEventsAsync(from, to, ownerId, ct);
                }

                events.AddRange(instanceEvents.Select(calendarEvent => calendarEvent with
                {
                    Id = $"{instance.Id:N}:{calendarEvent.Id}"
                }));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Calendar source instance {CalendarSourceInstanceId} ({PluginId}) failed for calendar owner {CalendarOwnerId}; continuing with remaining sources.",
                    instance.Id,
                    instance.PluginId,
                    ownerId);
            }
        }

        return events
            .Where(calendarEvent => calendarEvent.Start < to && calendarEvent.End > from)
            .OrderBy(calendarEvent => calendarEvent.Start)
            .ToList();
    }
}

