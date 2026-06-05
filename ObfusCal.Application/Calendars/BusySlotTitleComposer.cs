namespace ObfusCal.Application.Calendars;

/// <summary>
/// Composes outbound titles for peer sync and provider write-back without changing the internal source label.
/// </summary>
public static class BusySlotTitleComposer
{
    public static string? Compose(string? title, string? sourceName, string? fallbackTitle = null)
    {
        var baseTitle = string.IsNullOrWhiteSpace(title)
            ? Normalize(fallbackTitle)
            : title.Trim();

        if (string.IsNullOrWhiteSpace(baseTitle))
            return null;

        var normalizedSourceName = Normalize(sourceName);
        if (!string.IsNullOrWhiteSpace(normalizedSourceName)
            && baseTitle.EndsWith($" ({normalizedSourceName})", StringComparison.Ordinal))
        {
            return baseTitle;
        }

        return string.IsNullOrWhiteSpace(normalizedSourceName)
            ? baseTitle
            : $"{baseTitle} ({normalizedSourceName})";
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

