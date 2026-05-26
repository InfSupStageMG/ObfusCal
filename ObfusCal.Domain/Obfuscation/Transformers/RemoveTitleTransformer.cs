using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

/// <summary>
/// Clears the title of a calendar event to preserve privacy.
/// </summary>
public sealed class RemoveTitleTransformer : IObfuscationTransformerPlugin
{
    public string Id => "remove-title";
    public int Order => 100;

    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with
        {
            Title = string.Empty
        };
}

