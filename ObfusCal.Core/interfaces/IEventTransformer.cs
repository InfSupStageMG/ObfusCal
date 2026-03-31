using ObfusCal.Core.Models;

namespace ObfusCal.Core.Interfaces;

public interface IEventTransformer
{
    // returning null drops the event entirely
    CalendarEvent? Transform(CalendarEvent evt);
}