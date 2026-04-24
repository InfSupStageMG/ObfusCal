using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

public sealed class RemoveLocationTransformer : IObfuscationTransformer
{
    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with
        {
            Location = null
        };
}

