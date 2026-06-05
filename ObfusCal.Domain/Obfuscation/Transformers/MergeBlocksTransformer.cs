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
                if (next.SourceSlots?.Count > 0)
                    currentSources.AddRange(next.SourceSlots);
                else
                    currentSources.Add(next);

                // If titles are different, mark the top-level block as "Busy (Merged)"
                var newTitle = current.Title == next.Title ? current.Title : "Busy (Merged)";

                current = current with {
                    End = Max(current.End, next.End),
                    Title = newTitle,
                    Description = null,
                    AttendeeEmails = [],
                    Location = null,
                    SourceName = CombineSourceNames(currentSources)
                };
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

    private static string? CombineSourceNames(IReadOnlyList<BusySlot> sourceSlots)
    {
        var distinctSourceNames = sourceSlots.Select(slot => slot.SourceName)
            .Where(static sourceName => !string.IsNullOrWhiteSpace(sourceName))
            .Select(static sourceName => sourceName!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return distinctSourceNames.Count == 0
            ? null
            : string.Join(", ", distinctSourceNames);
    }

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

