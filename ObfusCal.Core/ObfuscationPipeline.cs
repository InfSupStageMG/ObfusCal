using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;
using Serilog;

namespace ObfusCal.Core;

public sealed class ObfuscationPipeline
{
    private readonly IEnumerable<IObfuscationTransformer> _transformers;

    public ObfuscationPipeline(IEnumerable<IObfuscationTransformer> transformers)
    {
        _transformers = transformers;
    }

    public IReadOnlyList<BusySlot> Process(IEnumerable<CalendarEvent> events)
    {
        var inputEvents = events as IReadOnlyCollection<CalendarEvent> ?? events.ToArray();

        Log.ForContext<ObfuscationPipeline>()
            .Information("Processed {EventCount} events through obfuscation pipeline", inputEvents.Count);

        return inputEvents
            .Select(calendarEvent => _transformers.Aggregate(calendarEvent, (current, transformer) => transformer.Transform(current)))
            .Select(calendarEvent => new BusySlot(calendarEvent.Id, calendarEvent.Start, calendarEvent.End))
            .ToList();
    }
}
