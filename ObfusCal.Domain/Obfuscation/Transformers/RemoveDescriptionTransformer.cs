using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

public sealed class RemoveDescriptionTransformer : IObfuscationTransformer
{
    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with
        {
            Description = null
        };
}

