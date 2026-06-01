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

    public static string CreateReadableForeground(string accentHex, string backgroundHex, double minimumContrast = 3)
    {
        var accent = NormalizeHexColorOrNull(accentHex)
            ?? throw new InvalidOperationException("An accent color is required to build a readable foreground.");
        var background = NormalizeHexColorOrNull(backgroundHex)
            ?? throw new InvalidOperationException("A background color is required to build a readable foreground.");

        if (GetContrastRatio(accent, background) >= minimumContrast)
            return accent;

        var backgroundLuminance = GetRelativeLuminance(background);
        var fallback = backgroundLuminance > 0.5 ? "#1F2937" : "#F9FAFB";
        if (GetContrastRatio(fallback, background) >= minimumContrast)
            return fallback;

        for (var step = 1; step <= 6; step++)
        {
            var candidate = Darken(accent, step * 0.12);
            if (GetContrastRatio(candidate, background) >= minimumContrast)
                return candidate;
        }

        return fallback;
    }

    private static int ParseHexComponent(string color, int startIndex)
        => int.Parse(color.AsSpan(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static int BlendWithWhite(int component, double whiteBlend)
        => (int)Math.Round(component + ((255 - component) * whiteBlend), MidpointRounding.AwayFromZero);

    private static string Darken(string color, double blackBlend)
    {
        var blend = Math.Clamp(blackBlend, 0, 1);
        var red = ParseHexComponent(color, 1);
        var green = ParseHexComponent(color, 3);
        var blue = ParseHexComponent(color, 5);

        return $"#{BlendWithBlack(red, blend):X2}{BlendWithBlack(green, blend):X2}{BlendWithBlack(blue, blend):X2}";
    }

    private static int BlendWithBlack(int component, double blackBlend)
        => (int)Math.Round(component * (1 - blackBlend), MidpointRounding.AwayFromZero);

    private static double GetContrastRatio(string firstColor, string secondColor)
    {
        var lighter = Math.Max(GetRelativeLuminance(firstColor), GetRelativeLuminance(secondColor));
        var darker = Math.Min(GetRelativeLuminance(firstColor), GetRelativeLuminance(secondColor));

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(string color)
    {
        var red = ToLinearChannel(ParseHexComponent(color, 1));
        var green = ToLinearChannel(ParseHexComponent(color, 3));
        var blue = ToLinearChannel(ParseHexComponent(color, 5));

        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    private static double ToLinearChannel(int channel)
    {
        var sRgb = channel / 255d;
        return sRgb <= 0.04045
            ? sRgb / 12.92
            : Math.Pow((sRgb + 0.055) / 1.055, 2.4);
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColorRegex();
}

