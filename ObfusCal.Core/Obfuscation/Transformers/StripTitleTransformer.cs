using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Core.Obfuscation.Transformers;

public class StripTitleTransformer : IEventTransformer
{
    public CalendarEvent? Transform(CalendarEvent evt) =>
        evt with { Title = "Busy", Description = null, Location = null };
}