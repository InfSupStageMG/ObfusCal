using ObfusCal.Core.Models;

namespace ObfusCal.Core.Interfaces;

/// <summary>
/// Transforms a collection of busy slots. This allows post-processing operations
/// that work on merged/aggregated data rather than individual events.
/// </summary>
public interface IBusySlotTransformer
{
    IReadOnlyList<BusySlot> Transform(IReadOnlyList<BusySlot> slots);
}

