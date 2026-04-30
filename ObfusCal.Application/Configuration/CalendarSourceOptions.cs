namespace ObfusCal.Application.Configuration;

public sealed class CalendarSourceOptions
{
    public const string SectionName = "Calendar";

    // Supported values: Graph, ICal, Mock.
    public string Provider { get; init; } = "Graph";
}

