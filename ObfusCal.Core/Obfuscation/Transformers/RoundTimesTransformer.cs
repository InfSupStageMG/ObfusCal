using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Core.Obfuscation.Transformers;

// Rounds start down and end up to the nearest 15 minutes
// Reduces fingerprinting of exact meeting times
public class RoundTimesTransformer : IEventTransformer
{
    private const int IntervalMinutes = 15;

    public CalendarEvent? Transform(CalendarEvent evt) => evt with
    {
        Start = RoundDown(evt.Start),
        End   = RoundUp(evt.End)
    };

    private static DateTimeOffset RoundDown(DateTimeOffset dt)
    {
        var excess = dt.TimeOfDay.TotalMinutes % IntervalMinutes;
        return dt.AddMinutes(-excess).AddSeconds(-dt.Second).AddMilliseconds(-dt.Millisecond);
    }

    private static DateTimeOffset RoundUp(DateTimeOffset dt)
    {
        var excess = dt.TimeOfDay.TotalMinutes % IntervalMinutes;
        return excess == 0 ? dt : dt.AddMinutes(IntervalMinutes - excess)
                                    .AddSeconds(-dt.Second)
                                    .AddMilliseconds(-dt.Millisecond);
    }
}