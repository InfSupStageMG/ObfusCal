using ObfusCal.Application.Calendars;
using ObfusCal.Application.UseCases.GetMergedFreeBusy;
using ObfusCal.Domain.Models;

namespace ObfusCal.Api.Components.Shared;

/// <summary>
/// Resolves presentation colors for merged free/busy labels and events.
/// </summary>
public sealed class MergedFreeBusyCalendarColorResolver
{
    private static readonly CalendarDisplayColor NeutralColor = BuildColor("#6B7280");
    private static readonly CalendarDisplayColor MixedSourcesColor = BuildColor("#475569");

    public CalendarDisplayColor BuildNeutralColor()
        => NeutralColor;

    public CalendarDisplayColor BuildMixedSourcesColor()
        => MixedSourcesColor;

    public Dictionary<string, CalendarDisplayColor> BuildLabelColors(IEnumerable<MergedFreeBusyResponse> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var labels = slots
            .SelectMany(GetAllLabelColorEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Label))
            .GroupBy(entry => entry.Label!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

        var labelColors = new Dictionary<string, CalendarDisplayColor>(StringComparer.Ordinal);
        for (var i = 0; i < labels.Count; i++)
        {
            var explicitAccent = labels[i]
                .Select(entry => TryNormalize(entry.ColorHex))
                .FirstOrDefault(color => color is not null);
            var accent = explicitAccent ?? CalendarColorPalette.DefaultAccentColors[i % CalendarColorPalette.DefaultAccentColors.Count];
            labelColors[labels[i].Key] = BuildColor(accent);
        }

        return labelColors;
    }

    public CalendarDisplayColor ResolveForEvent(
        MergedFreeBusyResponse slot,
        IReadOnlyDictionary<string, CalendarDisplayColor> labelColors)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(labelColors);

        if (slot.SourceSlots is not { Count: > 1 })
            return Resolve(slot.SourceLabel, slot.ColorHex, labelColors);

        var sourceColors = slot.SourceSlots
            .Select(source => Resolve(source.SourceLabel, source.ColorHex, labelColors))
            .Distinct()
            .ToList();

        return sourceColors.Count == 1
            ? sourceColors[0]
            : MixedSourcesColor;
    }

    public CalendarDisplayColor ResolveForSource(
        BusySlot slot,
        IReadOnlyDictionary<string, CalendarDisplayColor> labelColors)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(labelColors);

        return Resolve(slot.SourceLabel, slot.ColorHex, labelColors);
    }

    private CalendarDisplayColor Resolve(
        string? label,
        string? colorHex,
        IReadOnlyDictionary<string, CalendarDisplayColor> labelColors)
    {
        var explicitAccent = TryNormalize(colorHex);
        if (explicitAccent is not null)
            return BuildColor(explicitAccent);

        if (!string.IsNullOrWhiteSpace(label) && labelColors.TryGetValue(label, out var labelColor))
            return labelColor;

        return NeutralColor;
    }

    private static IEnumerable<(string? Label, string? ColorHex)> GetAllLabelColorEntries(MergedFreeBusyResponse slot)
    {
        yield return (slot.SourceLabel, slot.ColorHex);
        if (slot.SourceSlots is not { Count: > 0 })
            yield break;

        foreach (var source in slot.SourceSlots)
            yield return (source.SourceLabel, source.ColorHex);
    }

    private static string? TryNormalize(string? colorHex)
    {
        try
        {
            return CalendarColorPalette.NormalizeHexColorOrNull(colorHex);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static CalendarDisplayColor BuildColor(string accentHex)
    {
        var normalizedAccent = CalendarColorPalette.NormalizeHexColorOrNull(accentHex)
            ?? throw new InvalidOperationException("An accent color is required to build a display color.");
        var background = CalendarColorPalette.CreateBackgroundTint(normalizedAccent);
        return new CalendarDisplayColor(
            normalizedAccent,
            background,
            CalendarColorPalette.CreateReadableForeground(normalizedAccent, background));
    }
}

public readonly record struct CalendarDisplayColor(string Accent, string Bg, string Foreground);


