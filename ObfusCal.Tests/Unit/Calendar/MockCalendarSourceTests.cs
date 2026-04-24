using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Core.Interfaces;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class MockCalendarSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetEventsAsync_WithFourteenDayWindowStartingToday_ReturnsAtLeastThreeEvents()
    {
        var source = new MockCalendarSource();
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(14);

        var events = await source.GetEventsAsync(from, to, TestContext.CancellationToken);

        Assert.IsTrue(events.Count >= 3);
    }

    [TestMethod]
    public async Task GetEventsAsync_ReturnsOnlyEventsInsideRequestedWindow()
    {
        var source = new MockCalendarSource();
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(14);

        var events = await source.GetEventsAsync(from, to, TestContext.CancellationToken);

        Assert.IsTrue(events.All(calendarEvent => calendarEvent.Start >= from && calendarEvent.End <= to));
    }

    [TestMethod]
    public async Task GetEventsAsync_ReturnsAtLeastOneEventWithSensitiveFieldsPopulated()
    {
        var source = new MockCalendarSource();
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(14);

        var events = await source.GetEventsAsync(from, to, TestContext.CancellationToken);

        Assert.IsTrue(events.Any(calendarEvent =>
            !string.IsNullOrWhiteSpace(calendarEvent.Title)
            && !string.IsNullOrWhiteSpace(calendarEvent.Description)
            && calendarEvent.AttendeeEmails.Any(email => !string.IsNullOrWhiteSpace(email))
            && !string.IsNullOrWhiteSpace(calendarEvent.Location)));
    }

    [TestMethod]
    public async Task GetEventsAsync_ThrowsArgumentException_WhenFromIsAfterTo()
    {
        var source = new MockCalendarSource();
        var from = DateTimeOffset.UtcNow.AddDays(1);
        var to = DateTimeOffset.UtcNow;

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            source.GetEventsAsync(from, to, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetEventsAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        var source = new MockCalendarSource();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(14);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            source.GetEventsAsync(from, to, cts.Token));
    }

    [TestMethod]
    public async Task Application_ResolvesMockCalendarSource_AsActiveCalendarSource()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var scope = factory.Services.CreateScope();

        var source = scope.ServiceProvider.GetRequiredService<ICalendarSource>();

        Assert.IsInstanceOfType<MockCalendarSource>(source);
    }
}
