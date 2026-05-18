using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class AggregateCalendarSource(
    Guid ownerId,
    ICalendarSourceCatalog catalog,
    ICalendarSourceInstanceStore instanceStore,
    IServiceProvider serviceProvider,
    ObfuscationPipeline obfuscationPipeline,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService,
    ILogger<AggregateCalendarSource> logger)
    : ICalendarSource, ICalendarWriteBack
{
    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? calendarOwnerId = null,
        CancellationToken ct = default)
    {
        var enabledSources = await LoadEnabledSourcesAsync(ct);
        if (enabledSources.Count == 0)
            return [];

        var eventsByInstanceId = await LoadProjectedEventsByInstanceAsync(enabledSources, from, to, ct);

        return eventsByInstanceId
            .Values
            .SelectMany(events => events)
            .Where(calendarEvent => calendarEvent.Start < to && calendarEvent.End > from)
            .OrderBy(calendarEvent => calendarEvent.Start)
            .ToList();
    }

    public async Task WriteBackSlotsAsync(
        Guid calendarOwnerId,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default)
    {
        var enabledSources = await LoadEnabledSourcesAsync(ct);
        if (enabledSources.Count == 0)
            return;

        var eventsByInstanceId = await LoadProjectedEventsByInstanceAsync(enabledSources, windowStart, windowEnd, ct);
        var profile = await obfuscationProfileService.GetProfileAsync(
            calendarOwnerId,
            ObfuscationAuditContext.Client,
            ct);

        var wroteToDestination = false;
        foreach (var destination in enabledSources)
        {
            if (destination.Source is not ICalendarSourceInstanceWriteBack writeBack)
                continue;

            wroteToDestination = true;

            var eventsFromOtherSources = eventsByInstanceId
                .Where(entry => entry.Key != destination.Instance.Id)
                .SelectMany(entry => entry.Value)
                .ToList();

            var outboundSlots = obfuscationPipeline.Process(
                    eventsFromOtherSources,
                    calendarOwnerId.ToString(),
                    ObfuscationAuditContext.Client,
                    profile)
                .Concat(busySlots)
                .OrderBy(slot => slot.Start)
                .ToList();

            try
            {
                await writeBack.WriteBackSlotsAsync(
                    destination.Instance,
                    outboundSlots,
                    placeholderTitle,
                    windowStart,
                    windowEnd,
                    ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Write-back failed for calendar source instance {CalendarSourceInstanceId} ({PluginId}) for calendar owner {CalendarOwnerId}; continuing with remaining destinations.",
                    destination.Instance.Id,
                    destination.Instance.PluginId,
                    calendarOwnerId);
            }
        }

        if (!wroteToDestination)
        {
            logger.LogInformation(
                "No writable calendar source instances were available for calendar owner {CalendarOwnerId}; aggregate write-back skipped.",
                calendarOwnerId);
        }
    }

    private async Task<IReadOnlyList<EnabledSourceInstance>> LoadEnabledSourcesAsync(CancellationToken ct)
    {
        var instances = await instanceStore.ListAsync(ownerId, ct);

        return instances
            .Where(instance => instance.IsEnabled)
            .Select(instance => new
            {
                Instance = instance,
                Plugin = catalog.GetPlugin(instance.PluginId)
            })
            .Where(entry => entry.Plugin is not null)
            .Select(entry => new EnabledSourceInstance(
                entry.Instance,
                (ICalendarSource)serviceProvider.GetRequiredService(entry.Plugin!.ImplementationType)))
            .ToList();
    }

    private async Task<Dictionary<Guid, IReadOnlyList<CalendarEvent>>> LoadProjectedEventsByInstanceAsync(
        IReadOnlyList<EnabledSourceInstance> enabledSources,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var eventsByInstanceId = new Dictionary<Guid, IReadOnlyList<CalendarEvent>>();

        foreach (var sourceInstance in enabledSources)
        {
            try
            {
                IReadOnlyList<CalendarEvent> instanceEvents;
                if (sourceInstance.Source is ICalendarSourceInstanceHandler instanceHandler)
                {
                    instanceEvents = await instanceHandler.GetEventsAsync(sourceInstance.Instance, from, to, ct);
                }
                else
                {
                    instanceEvents = await sourceInstance.Source.GetEventsAsync(from, to, ownerId, ct);
                }

                eventsByInstanceId[sourceInstance.Instance.Id] = instanceEvents
                    .Select(calendarEvent => calendarEvent with
                    {
                        Id = $"{sourceInstance.Instance.Id:N}:{calendarEvent.Id}"
                    })
                    .ToList();
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
                    sourceInstance.Instance.Id,
                    sourceInstance.Instance.PluginId,
                    ownerId);
            }
        }

        return eventsByInstanceId;
    }

    private sealed record EnabledSourceInstance(
        CalendarSourceInstanceContext Instance,
        ICalendarSource Source);
}

