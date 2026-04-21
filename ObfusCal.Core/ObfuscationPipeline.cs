using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Core;

public sealed class ObfuscationPipeline
{
    private readonly IEnumerable<IObfuscationTransformer> _transformers;

    public ObfuscationPipeline(IEnumerable<IObfuscationTransformer> transformers)
    {
        _transformers = transformers;
    }
    
    public IReadOnlyList<BusySlot> Process(IEnumerable<CalendarEvent> events) =>
        events
            .Select(e => _transformers.Aggregate(e, (current, transformer) => transformer.Transform(current)))
            .Select(e => new BusySlot(e.Id, e.Start, e.End))
            .ToList();
}
