using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation;

public interface IObfuscationTransformer
{
    CalendarEvent Transform(CalendarEvent calendarEvent);
}

