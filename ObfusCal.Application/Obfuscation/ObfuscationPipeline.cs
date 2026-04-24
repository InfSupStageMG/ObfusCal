using Microsoft.Extensions.Logging;
using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation;

namespace ObfusCal.Application.Obfuscation;

public sealed class ObfuscationPipeline(
    IEnumerable<IObfuscationTransformer> transformers,
    IEnumerable<IBusySlotTransformer> slotTransformers,
    ILogger<ObfuscationPipeline> logger)
{
    public IReadOnlyList<BusySlot> Process(IEnumerable<CalendarEvent> events)
    {
        var inputEvents = events as IReadOnlyCollection<CalendarEvent> ?? events.ToArray();

        logger.LogInformation("Processed {EventCount} events through obfuscation pipeline", inputEvents.Count);

        var slots = inputEvents
            .Select(calendarEvent => transformers.Aggregate(calendarEvent, (current, transformer) => transformer.Transform(current)))
            .Select(calendarEvent => new BusySlot(calendarEvent.Id, calendarEvent.Start, calendarEvent.End))
            .ToList();

        logger.LogDebug("Generated {BusySlotCount} busy slots after event obfuscation", slots.Count);

        // Apply slot transformers (e.g., merging)
        var finalSlots = slotTransformers.Aggregate((IReadOnlyList<BusySlot>)slots, (current, transformer) => transformer.Transform(current));

        logger.LogInformation("Completed obfuscation pipeline with {FinalSlotCount} final busy slots", finalSlots.Count);

        return finalSlots;
    }
}

