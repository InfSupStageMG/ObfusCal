using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

/// <summary>
/// Strips the source label from a calendar event to prevent calendar origin information
/// from crossing domain boundaries into peer-facing output.
/// </summary>
public sealed class RemoveSourceLabelTransformer : IObfuscationTransformerPlugin
{
    public string Id => "remove-source-label";
    public int Order => 100;

    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with { SourceLabel = null, ColorHex = null };
}
