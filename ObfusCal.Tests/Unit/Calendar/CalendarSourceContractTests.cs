using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class CalendarSourceContractTests
{
    [TestMethod]
    public async Task GetEventsAsync_ReturnsOnlyEventsWithinRequestedWindow()
    {
        // Arrange
        var source = new StubCalendarSource();
        var from = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero);

        // Act
        var results = await source.GetEventsAsync(from, to);

        // Assert
        Assert.IsTrue(results.All(e => e.Start >= from && e.End <= to));
    }
}

file class StubCalendarSource : ICalendarSource
{
    private static readonly List<CalendarEvent> Events =
    [
        new("1", "Inside",  null,
            new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 12, 10, 0, 0, TimeSpan.Zero),
            [], null),
        new("2", "Outside", null,
            new DateTimeOffset(2026, 1, 25, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 25, 10, 0, 0, TimeSpan.Zero),
            [], null),
    ];

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CalendarEvent>>(
            Events.Where(e => e.Start >= from && e.End <= to).ToList());
}
