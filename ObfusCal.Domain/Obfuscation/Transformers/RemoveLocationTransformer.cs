using ObfusCal.Domain.Models;

namespace ObfusCal.Domain.Obfuscation.Transformers;

public sealed class RemoveLocationTransformer : IObfuscationTransformerPlugin
{
    public string Id => "remove-location";
    public int Order => 300;

    public CalendarEvent Transform(CalendarEvent calendarEvent) =>
        calendarEvent with
        {
            Location = null
        };
}

