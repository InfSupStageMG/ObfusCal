using ObfusCal.Core.Models;

namespace ObfusCal.Core.Interfaces;

public interface IObfuscationTransformer
{
    CalendarEvent Transform(CalendarEvent calendarEvent);
}