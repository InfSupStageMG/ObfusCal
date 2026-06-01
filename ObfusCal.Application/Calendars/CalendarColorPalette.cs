using System.Globalization;
using System.Text.RegularExpressions;

namespace ObfusCal.Application.Calendars;

/// <summary>
/// Provides normalized calendar accent colors, a default palette, and subtle background tints derived from those accents.
/// </summary>
public static partial class CalendarColorPalette
{
    private static readonly string[] AccentColors =
    [
        "#2563EB",
        "#0F766E",
        "#7C3AED",
        "#EA580C",
        "#BE123C",
        "#0891B2",
        "#4F46E5",
        "#65A30D",
        "#C026D3",
        "#B45309"
    ];

    public static IReadOnlyList<string> DefaultAccentColors => AccentColors;

    public static string? NormalizeHexColorOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('#'))
            trimmed = $"#{trimmed}";

        if (!HexColorRegex().IsMatch(trimmed))
            throw new InvalidOperationException("Calendar color must be a valid hex color such as #2563EB.");

        if (trimmed.Length == 4)
        {
            trimmed = string.Create(7, trimmed, static (span, source) =>
            {
                span[0] = '#';
                span[1] = source[1];
                span[2] = source[1];
                span[3] = source[2];
                span[4] = source[2];
                span[5] = source[3];
                span[6] = source[3];
            });
        }

        return trimmed.ToUpperInvariant();
    }

    public static string CreateBackgroundTint(string accentHex, double whiteBlend = 0.88)
    {
        var normalized = NormalizeHexColorOrNull(accentHex)
            ?? throw new InvalidOperationException("An accent color is required to build a background tint.");

        var blend = Math.Clamp(whiteBlend, 0, 1);
        var red = ParseHexComponent(normalized, 1);
        var green = ParseHexComponent(normalized, 3);
        var blue = ParseHexComponent(normalized, 5);

        return $"#{BlendWithWhite(red, blend):X2}{BlendWithWhite(green, blend):X2}{BlendWithWhite(blue, blend):X2}";
    }

    private static int ParseHexComponent(string color, int startIndex)
        => int.Parse(color.AsSpan(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static int BlendWithWhite(int component, double whiteBlend)
        => (int)Math.Round(component + ((255 - component) * whiteBlend), MidpointRounding.AwayFromZero);

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColorRegex();
}

