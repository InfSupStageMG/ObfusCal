namespace ObfusCal.Application.Configuration;

public sealed class CalendarSourceOptions
{
    public const string SectionName = "Calendar";

    // Calendar source plugin id to use when a calendar owner has not selected a specific provider.
    public string Provider { get; init; } = "graph";
}

