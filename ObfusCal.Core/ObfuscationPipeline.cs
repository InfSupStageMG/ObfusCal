using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

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
        var slots = events
            .Select(e => _transformers.Aggregate(e, (current, transformer) => transformer.Transform(current)))
            .Select(e => new BusySlot(e.Id, e.Start, e.End))
            .ToList();

        // Apply slot transformers (e.g., merging)
        return _slotTransformers.Aggregate((IReadOnlyList<BusySlot>)slots, (current, transformer) => transformer.Transform(current));
    }
}
