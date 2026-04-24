using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

public sealed class RemoveAttendeesTransformer : IObfuscationTransformer
{
    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with
        {
            AttendeeEmails = []
        };
}

