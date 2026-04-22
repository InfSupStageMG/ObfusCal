using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Core.Obfuscation.Transformers;

/// <summary>
/// Rounds event start times down and end times up to the nearest 15-minute boundary.
/// This prevents fingerprinting of meetings based on exact start/end times and durations.
/// </summary>
public sealed class RoundTimesTransformer : IObfuscationTransformer
{
    private const int RoundingIntervalMinutes = 15;

    public CalendarEvent Transform(CalendarEvent calendarEvent)
    {
        var roundedStart = RoundDown(calendarEvent.Start);
        var roundedEnd = RoundUp(calendarEvent.End);

        return calendarEvent with
        {
            Start = roundedStart,
            End = roundedEnd
        };
    }
    
    private static DateTimeOffset RoundDown(DateTimeOffset dateTime)
    {
        var totalMinutes = dateTime.TimeOfDay.TotalMinutes;
        var roundedMinutes = Math.Floor(totalMinutes / RoundingIntervalMinutes) * RoundingIntervalMinutes;
        var timeSpan = TimeSpan.FromMinutes(roundedMinutes);
        var roundedDateTime = dateTime.Date.Add(timeSpan);
        return new DateTimeOffset(roundedDateTime, dateTime.Offset);
    }
    
    private static DateTimeOffset RoundUp(DateTimeOffset dateTime)
    {
        var totalMinutes = dateTime.TimeOfDay.TotalMinutes;
        var roundedMinutes = Math.Ceiling(totalMinutes / RoundingIntervalMinutes) * RoundingIntervalMinutes;
        
        // Handle case where rounding up crosses midnight
        if (roundedMinutes >= 24 * 60)
        {
            roundedMinutes -= 24 * 60;
            var roundedDateTime = dateTime.Date.AddDays(1).Add(TimeSpan.FromMinutes(roundedMinutes));
            return new DateTimeOffset(roundedDateTime, dateTime.Offset);
        }

        var timeSpan = TimeSpan.FromMinutes(roundedMinutes);
        var roundedDateTime2 = dateTime.Date.Add(timeSpan);
        return new DateTimeOffset(roundedDateTime2, dateTime.Offset);
    }
}


