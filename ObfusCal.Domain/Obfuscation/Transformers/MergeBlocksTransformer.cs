using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

/// <summary>
/// Merges overlapping and adjacent busy slots into single continuous blocks.
/// This prevents fingerprinting of schedules based on the frequency and boundaries
/// of individual busy blocks.
/// </summary>
public sealed class MergeBlocksTransformer : IBusySlotTransformer
{
    public IReadOnlyList<BusySlot> Transform(IReadOnlyList<BusySlot> slots)
    {
        if (slots.Count == 0)
            return slots;

        var sorted = slots.OrderBy(s => s.Start).ToList();
        var merged = new List<BusySlot>();

        var current = sorted[0];
        for (var i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];

            // Merge if overlapping or adjacent (next starts at or before current ends)
            if (next.Start <= current.End)
            {
                current = current with { End = Max(current.End, next.End) };
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged.AsReadOnly();
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) =>
        a > b ? a : b;
}

