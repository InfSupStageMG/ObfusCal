using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;
using Serilog;

namespace ObfusCal.Core;

public sealed class ObfuscationPipeline
{
    private readonly IEnumerable<IObfuscationTransformer> _transformers;
    private readonly IEnumerable<IBusySlotTransformer> _slotTransformers;

    public ObfuscationPipeline(IEnumerable<IObfuscationTransformer> transformers, IEnumerable<IBusySlotTransformer> slotTransformers)
    {
        _transformers = transformers;
        _slotTransformers = slotTransformers;
    }

    public IReadOnlyList<BusySlot> Process(IEnumerable<CalendarEvent> events)
    {
        var inputEvents = events as IReadOnlyCollection<CalendarEvent> ?? events.ToArray();

        Log.ForContext<ObfuscationPipeline>()
            .Information("Processed {EventCount} events through obfuscation pipeline", inputEvents.Count);

        var slots = inputEvents
            .Select(calendarEvent => _transformers.Aggregate(calendarEvent, (current, transformer) => transformer.Transform(current)))
            .Select(calendarEvent => new BusySlot(calendarEvent.Id, calendarEvent.Start, calendarEvent.End))
            .ToList();

        Log.ForContext<ObfuscationPipeline>()
            .ForContext("InputSlotCount", slots.Count)
            .Debug("Generated {BusySlotCount} busy slots after event obfuscation", slots.Count);

        // Apply slot transformers (e.g., merging)
        var finalSlots = _slotTransformers.Aggregate((IReadOnlyList<BusySlot>)slots, (current, transformer) => transformer.Transform(current));

        Log.ForContext<ObfuscationPipeline>()
            .ForContext("FinalSlotCount", finalSlots.Count)
            .Information("Completed obfuscation pipeline with {FinalSlotCount} final busy slots", finalSlots.Count);

        return finalSlots;
    }
}
