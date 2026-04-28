using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class IcalFeedCalendarSourceTests
{
    [TestMethod]
    public async Task GetEventsAsync_ReturnsParsedEventsWithinRequestedWindow()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, SampleIcs),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId, CancellationToken.None);

        Assert.HasCount(1, events);
        Assert.AreEqual("event-1", events[0].Id);
        Assert.AreEqual("Daily Sync", events[0].Title);
        Assert.AreEqual("alice@example.test", events[0].AttendeeEmails.Single());
        Assert.AreEqual(new DateTimeOffset(2026, 4, 24, 9, 0, 0, TimeSpan.Zero), events[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 24, 10, 0, 0, TimeSpan.Zero), events[0].End);
    }

    [TestMethod]
    public async Task GetEventsAsync_AggregatesEventsFromMultipleFeeds()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext,
            "https://calendar.example.test/feed1.ics",
            "https://calendar.example.test/feed2.ics");

        // Both feeds return the same ICS but different UIDs
        var feed1Ics = SampleIcs;
        var feed2Ics = SampleIcs.Replace("event-1", "event-from-feed-2");

        var callCount = 0;
        var source = new IcalFeedCalendarSource(
            CreateDynamicHttpClient(_ =>
            {
                callCount++;
                return (HttpStatusCode.OK, callCount == 1 ? feed1Ics : feed2Ics);
            }),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId, CancellationToken.None);

        Assert.HasCount(2, events);
        Assert.Contains(e => e.Id == "event-1", events);
        Assert.Contains(e => e.Id == "event-from-feed-2", events);
    }

    [TestMethod]
    public async Task GetEventsAsync_ContinuesWithOtherFeeds_WhenOneFeedFails()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext,
            "https://calendar.example.test/broken.ics",
            "https://calendar.example.test/good.ics");

        var callCount = 0;
        var source = new IcalFeedCalendarSource(
            CreateDynamicHttpClient(_ =>
            {
                callCount++;
                return callCount == 1
                    ? (HttpStatusCode.InternalServerError, "error")
                    : (HttpStatusCode.OK, SampleIcs);
            }),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId, CancellationToken.None);

        // Only the good feed's event should be returned
        Assert.HasCount(1, events);
        Assert.AreEqual("event-1", events[0].Id);
    }

    [TestMethod]
    public async Task GetEventsAsync_ReturnsEmptyList_WhenAllFeedsFail()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.InternalServerError, "boom"),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId, CancellationToken.None);

        Assert.IsEmpty(events);
    }

    [TestMethod]
    public async Task GetEventsAsync_FallsBackToMockSource_WhenNoFeedsConfigured()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext); // no feeds

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, string.Empty),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(14);

        var events = await source.GetEventsAsync(from, to, ownerId, CancellationToken.None);

        Assert.IsGreaterThanOrEqualTo(3, events.Count);
    }

    // --- helpers ---

    private static async Task<Guid> SeedOwnerWithFeedsAsync(AppDbContext dbContext, params string[] feedUrls)
    {
        var owner = new CalendarOwner { Id = Guid.NewGuid(), Name = "Test Owner" };
        dbContext.CalendarOwners.Add(owner);

        foreach (var url in feedUrls)
            dbContext.CalendarOwnerICalFeeds.Add(new CalendarOwnerICalFeed
            {
                Id = Guid.NewGuid(),
                CalendarOwnerId = owner.Id,
                FeedUrl = url
            });

        await dbContext.SaveChangesAsync();
        return owner.Id;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string content)
        => CreateDynamicHttpClient(_ => (statusCode, content));

    private static HttpClient CreateDynamicHttpClient(
        Func<HttpRequestMessage, (HttpStatusCode StatusCode, string Content)> handler)
        => new(new DelegatingHttpMessageHandler(handler));

    private sealed class DelegatingHttpMessageHandler(
        Func<HttpRequestMessage, (HttpStatusCode StatusCode, string Content)> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (statusCode, content) = handler(request);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
        }
    }

    private const string SampleIcs = "BEGIN:VCALENDAR\r\n" +
                                     "VERSION:2.0\r\n" +
                                     "BEGIN:VEVENT\r\n" +
                                     "UID:event-1\r\n" +
                                     "SUMMARY:Daily Sync\r\n" +
                                     "DESCRIPTION:Project sync\r\n" +
                                     "DTSTART:20260424T090000Z\r\n" +
                                     "DTEND:20260424T100000Z\r\n" +
                                     "ATTENDEE:mailto:alice@example.test\r\n" +
                                     "LOCATION:Room A\r\n" +
                                     "END:VEVENT\r\n" +
                                     "BEGIN:VEVENT\r\n" +
                                     "UID:event-2\r\n" +
                                     "SUMMARY:Outside Window\r\n" +
                                     "DTSTART:20260515T090000Z\r\n" +
                                     "DTEND:20260515T100000Z\r\n" +
                                     "END:VEVENT\r\n" +
                                     "END:VCALENDAR\r\n";
}
