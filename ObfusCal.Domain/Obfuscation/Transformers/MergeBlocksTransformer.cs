using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

/// <summary>
/// Merges overlapping and adjacent busy slots into single continuous blocks.
/// This prevents fingerprinting of schedules based on the frequency and boundaries
/// of individual busy blocks.
/// </summary>
public sealed class MergeBlocksTransformer : IBusySlotTransformerPlugin
{
    public string Id => "merge-blocks";
    public int Order => 100;

    public IReadOnlyList<BusySlot> Transform(IReadOnlyList<BusySlot> slots)
    {
        if (slots.Count == 0)
            return slots;

        var sorted = slots.OrderBy(s => s.Start).ToList();
        var merged = new List<BusySlot>();

        var current = sorted[0];
        var currentSources = new List<BusySlot> { current };

        for (var i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];

            // Merge if overlapping or adjacent (next starts at or before current ends)
            if (next.Start <= current.End)
            {
                // Accumulate source slots: flatten any existing SourceSlots and add the slot itself
                if (next.SourceSlots?.Count > 0)
                    currentSources.AddRange(next.SourceSlots);
                else
                    currentSources.Add(next);

                current = current with { End = Max(current.End, next.End) };
            }
            else
            {
                // Finalize current merged slot with its sources
                var finalSlot = current with
                {
                    SourceSlots = NormalizeSourceSlotsToMergedWindow(currentSources, current.Start, current.End)
                };
                merged.Add(finalSlot);

                current = next;
                currentSources = new List<BusySlot> { next };
            }
        }

        // Add final merged slot
        var lastFinalSlot = current with
        {
            SourceSlots = NormalizeSourceSlotsToMergedWindow(currentSources, current.Start, current.End)
        };
        merged.Add(lastFinalSlot);

        return merged.AsReadOnly();
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) =>
        a > b ? a : b;

    private static IReadOnlyList<BusySlot> NormalizeSourceSlotsToMergedWindow(
        IReadOnlyList<BusySlot> sourceSlots,
        DateTimeOffset mergedStart,
        DateTimeOffset mergedEnd)
    {
        return sourceSlots
            .Select(source => source with { Start = mergedStart, End = mergedEnd, SourceSlots = null })
            .ToList()
            .AsReadOnly();
    }
}

