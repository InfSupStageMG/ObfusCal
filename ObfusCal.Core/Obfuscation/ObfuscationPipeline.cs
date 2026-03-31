using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Core.Obfuscation;

public class ObfuscationPipeline(IEnumerable<IEventTransformer> transformers)
{
    public IReadOnlyList<BusySlot> Process(IReadOnlyList<CalendarEvent> events) =>
        events
            .Select(e => transformers.Aggregate(
                (CalendarEvent?)e,
                (current, transformer) => current is null ? null : transformer.Transform(current)))
            .OfType<CalendarEvent>()
            .Select(e => new BusySlot(e.Start, e.End))
            .OrderBy(s => s.Start)
            .ToList();
}