using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

public sealed class RemoveAttendeesTransformer : IObfuscationTransformerPlugin
{
    public string Id => "remove-attendees";
    public int Order => 400;

    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with
        {
            AttendeeEmails = []
        };
}

