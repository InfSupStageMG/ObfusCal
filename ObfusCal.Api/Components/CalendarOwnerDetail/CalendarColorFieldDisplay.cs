using ObfusCal.Application.Calendars;

namespace ObfusCal.Api.Components.CalendarOwnerDetail;

/// <summary>
/// Provides display helpers for the calendar color selection UI.
/// </summary>
public static class CalendarColorFieldDisplay
{
    public static IReadOnlyList<string> PresetColors => CalendarColorPalette.DefaultAccentColors;

    public static string ResolvePreviewColor(string? colorHex, string? automaticColorHex = null)
    {
        return TryNormalize(colorHex)
               ?? TryNormalize(automaticColorHex)
               ?? CalendarColorPalette.DefaultAccentColors[0];
    }

    public static string ResolveAutomaticColor(string? label, IEnumerable<string> labels)
    {
        if (labels is null)
            throw new ArgumentNullException(nameof(labels));

        var orderedLabels = labels
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(static candidate => candidate.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static candidate => candidate, StringComparer.Ordinal)
            .ToList();

        if (orderedLabels.Count == 0)
            return CalendarColorPalette.DefaultAccentColors[0];

        var normalizedLabel = label?.Trim();
        var index = orderedLabels.FindIndex(candidate =>
            string.Equals(candidate, normalizedLabel, StringComparison.Ordinal));

        return index >= 0
            ? CalendarColorPalette.DefaultAccentColors[index % CalendarColorPalette.DefaultAccentColors.Count]
            : CalendarColorPalette.DefaultAccentColors[0];
    }

    public static string DescribeSelection(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
            return "Automatic palette";

        var normalized = TryNormalize(colorHex);
        return normalized ?? "Custom color";
    }


    public static bool IsAutomatic(string? colorHex)
        => string.IsNullOrWhiteSpace(colorHex);

    public static bool IsPresetSelected(string? colorHex, string presetColor)
        => string.Equals(TryNormalize(colorHex), TryNormalize(presetColor), StringComparison.OrdinalIgnoreCase);

    public static string? TryNormalize(string? colorHex)
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
}

