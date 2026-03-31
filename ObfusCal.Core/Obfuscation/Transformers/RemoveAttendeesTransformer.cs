using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Core.Obfuscation.Transformers;

public class RemoveAttendeesTransformer : IEventTransformer
{
    public CalendarEvent? Transform(CalendarEvent evt) =>
        evt with { AttendeeEmails = [] };
}