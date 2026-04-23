using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;
using Serilog;

namespace ObfusCal.Core;

public sealed class ObfuscationPipeline(
    IEnumerable<IObfuscationTransformer> transformers,
    IEnumerable<IBusySlotTransformer> slotTransformers,
    ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<ObfuscationPipeline>();

    public IReadOnlyList<BusySlot> Process(IEnumerable<CalendarEvent> events)
    {
        var inputEvents = events as IReadOnlyCollection<CalendarEvent> ?? events.ToArray();

        _logger.Information("Processed {EventCount} events through obfuscation pipeline", inputEvents.Count);

        var slots = inputEvents
            .Select(calendarEvent => transformers.Aggregate(calendarEvent, (current, transformer) => transformer.Transform(current)))
            .Select(calendarEvent => new BusySlot(calendarEvent.Id, calendarEvent.Start, calendarEvent.End))
            .ToList();

        _logger.ForContext("InputSlotCount", slots.Count)
            .Debug("Generated {BusySlotCount} busy slots after event obfuscation", slots.Count);

        // Apply slot transformers (e.g., merging)
        var finalSlots = slotTransformers.Aggregate((IReadOnlyList<BusySlot>)slots, (current, transformer) => transformer.Transform(current));

        _logger.ForContext("FinalSlotCount", finalSlots.Count)
            .Information("Completed obfuscation pipeline with {FinalSlotCount} final busy slots", finalSlots.Count);

        return finalSlots;
    }
}
