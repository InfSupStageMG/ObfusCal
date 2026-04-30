using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

public sealed class RemoveDescriptionTransformer : IObfuscationTransformerPlugin
{
    public string Id => "remove-description";
    public int Order => 200;

    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with
        {
            Description = null
        };
}

