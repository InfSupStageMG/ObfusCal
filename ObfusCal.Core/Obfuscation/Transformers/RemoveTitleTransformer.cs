using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Core.Obfuscation.Transformers;

public sealed class RemoveTitleTransformer : IObfuscationTransformer
{
    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with
        {
            Title = string.Empty
        };
}
