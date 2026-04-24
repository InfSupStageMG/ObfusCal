using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

public sealed class RemoveTitleTransformer : IObfuscationTransformer
{
    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with
        {
            Title = string.Empty
        };
}

