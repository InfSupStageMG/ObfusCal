namespace ObfusCal.Application.Configuration;

public sealed class ICloudCalendarOptions
{
    public const string SectionName = "ICloudCalendar";

    public string BaseUrl { get; init; } = "https://caldav.icloud.com/";
    public int ReadinessProbeLookAheadDays { get; init; } = 1;
}



