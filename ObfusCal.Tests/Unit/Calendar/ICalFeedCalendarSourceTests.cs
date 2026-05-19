using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

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

    [TestMethod]
    public async Task GetEventsAsync_ThrowsArgumentException_WhenFromIsAfterTo()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, SampleIcs),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => source.GetEventsAsync(from, to, ownerId));
    }

    [TestMethod]
    public async Task GetEventsAsync_DoesNotThrow_WhenFromEqualsTo()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, SampleIcs),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var same = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        // from == to should NOT throw (empty window is valid)
        var events = await source.GetEventsAsync(same, same, ownerId);
        Assert.IsEmpty(events);
    }

    [TestMethod]
    public async Task GetEventsAsync_ThrowsOperationCanceled_WhenTokenCancelled()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, SampleIcs),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var from = new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => source.GetEventsAsync(from, to, ownerId, cts.Token));
    }

    [TestMethod]
    public async Task GetEventsAsync_ReturnsEventsOrderedByStartAscending()
    {
        var ics = "BEGIN:VCALENDAR\r\n" +
                  "VERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:late\r\nSUMMARY:Late\r\nDTSTART:20260424T140000Z\r\nDTEND:20260424T150000Z\r\nEND:VEVENT\r\n" +
                  "BEGIN:VEVENT\r\nUID:early\r\nSUMMARY:Early\r\nDTSTART:20260424T080000Z\r\nDTEND:20260424T090000Z\r\nEND:VEVENT\r\n" +
                  "BEGIN:VEVENT\r\nUID:mid\r\nSUMMARY:Mid\r\nDTSTART:20260424T110000Z\r\nDTEND:20260424T120000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(3, events);
        Assert.AreEqual("early", events[0].Id);
        Assert.AreEqual("mid", events[1].Id);
        Assert.AreEqual("late", events[2].Id);
    }

    [TestMethod]
    public async Task GetEventsAsync_ExcludesEventStartingExactlyAtTo()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:at-boundary\r\nSUMMARY:At Boundary\r\nDTSTART:20260425T000000Z\r\nDTEND:20260425T010000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero); // Event starts exactly at to

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.IsEmpty(events, "Event starting exactly at 'to' must be excluded (e.Start < to)");
    }

    [TestMethod]
    public async Task GetEventsAsync_ExcludesEventEndingExactlyAtFrom()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:ends-at-from\r\nSUMMARY:Ends At From\r\nDTSTART:20260423T230000Z\r\nDTEND:20260424T000000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero); // Event ends exactly at from
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.IsEmpty(events, "Event ending exactly at 'from' must be excluded (e.End > from)");
    }

    [TestMethod]
    public async Task GetEventsAsync_ReturnsEmptyForNonExistentOwner()
    {
        await using var dbContext = CreateDbContext();

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, SampleIcs),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, Guid.NewGuid());

        Assert.IsEmpty(events);
    }

    [TestMethod]
    public async Task GetEventsAsync_WithNullOwnerId_FallsBackToMockSource()
    {
        await using var dbContext = CreateDbContext();

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, string.Empty),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(14);

        var events = await source.GetEventsAsync(from, to, calendarOwnerId: null);

        Assert.IsTrue(events.Count > 0, "Should fall back to MockCalendarSource");
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesEventWithoutUid_GeneratesId()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nSUMMARY:No UID\r\nDTSTART:20260424T090000Z\r\nDTEND:20260424T100000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.IsFalse(string.IsNullOrWhiteSpace(events[0].Id), "Should generate an ID when UID is missing");
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesEventWithoutSummary_DefaultsToBusy()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:no-summary\r\nDTSTART:20260424T090000Z\r\nDTEND:20260424T100000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.AreEqual("Busy", events[0].Title, "Should default to 'Busy' when SUMMARY is missing");
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesEventWithoutDescription_ReturnsNullDescription()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:no-desc\r\nSUMMARY:Test\r\nDTSTART:20260424T090000Z\r\nDTEND:20260424T100000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.IsNull(events[0].Description, "Should be null when DESCRIPTION is missing");
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesAttendeeWithMailtoPrefix_StripsPrefix()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:attendee-test\r\nSUMMARY:Test\r\nDTSTART:20260424T090000Z\r\nDTEND:20260424T100000Z\r\n" +
                  "ATTENDEE:mailto:stripped@example.com\r\n" +
                  "END:VEVENT\r\nEND:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.AreEqual("stripped@example.com", events[0].AttendeeEmails[0],
            "mailto: prefix should be stripped from attendee emails");
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesAttendeeWithoutMailtoPrefix_KeepsOriginal()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:attendee-no-mailto\r\nSUMMARY:Test\r\nDTSTART:20260424T090000Z\r\nDTEND:20260424T100000Z\r\n" +
                  "ATTENDEE:plain@example.com\r\n" +
                  "END:VEVENT\r\nEND:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.AreEqual("plain@example.com", events[0].AttendeeEmails[0]);
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesDateOnlyDtstart()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:date-only\r\nSUMMARY:All Day\r\nDTSTART:20260424\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 23, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 26, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.AreEqual("date-only", events[0].Id);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero), events[0].Start);
        // date-only DTSTART without DTEND should default to +1 day
        Assert.AreEqual(new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero), events[0].End);
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesAllDayEventWithValueDateAndExplicitDtend()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:all-day-value-date\r\nSUMMARY:Holiday\r\n" +
                  "DTSTART;VALUE=DATE:20260510\r\n" +
                  "DTEND;VALUE=DATE:20260511\r\n" +
                  "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var events = await GetParsedEventsAsync(ics,
            new DateTimeOffset(2026, 5, 9, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero));

        Assert.HasCount(1, events);
        Assert.AreEqual("all-day-value-date", events[0].Id);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero), events[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero), events[0].End);
    }

    [TestMethod]
    public async Task GetEventsAsync_AllDayEventWithSameDateDtstartDtend_NormalizesToOneDay()
    {
        // Some providers emit DTSTART/DTEND with the same DATE value for all-day events.
        // We normalize this to one day so full-day events are not dropped.
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:all-day-same-date\r\nSUMMARY:Local Holiday\r\n" +
                  "DTSTART;VALUE=DATE:20260510\r\n" +
                  "DTEND;VALUE=DATE:20260510\r\n" +
                  "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var events = await GetParsedEventsAsync(ics,
            new DateTimeOffset(2026, 5, 9, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero));

        Assert.HasCount(1, events);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero), events[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero), events[0].End);
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesDtstartWithoutZ_AssumedUtc()
    {
        // Tests non-UTC datetime format parsing (yyyyMMdd'T'HHmmss without Z)
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:no-z\r\nSUMMARY:No Z\r\nDTSTART:20260424T090000\r\nDTEND:20260424T100000\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 24, 9, 0, 0, TimeSpan.Zero), events[0].Start);
    }

    [TestMethod]
    public async Task GetEventsAsync_WithLineFolding_ParsesContinuationLines()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:folded-uid\r\nSUMMARY:This is a very long summary\r\n" +
                  " that continues on the next line\r\n" +
                  "DTSTART:20260424T090000Z\r\nDTEND:20260424T100000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.IsTrue(events[0].Title.Contains("that continues on the next line"),
            "Line-folded continuation should be unfolded");
    }

    [TestMethod]
    public async Task GetEventsAsync_WithTabLineFolding_ParsesContinuationLines()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:tab-folded\r\nSUMMARY:Tab folded\r\n" +
                  "\tcontinued with tab\r\n" +
                  "DTSTART:20260424T090000Z\r\nDTEND:20260424T100000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.IsTrue(events[0].Title.Contains("continued with tab"),
            "Tab-folded continuation should be unfolded");
    }

    [TestMethod]
    public async Task GetEventsAsync_SkipsLineWithColonAtPosition0()
    {
        // A line like ":value" (colon at index 0) has an empty key and should be skipped
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:colon-test\r\nSUMMARY:Test\r\n" +
                  ":orphan-value\r\n" +
                  "DTSTART:20260424T090000Z\r\nDTEND:20260424T100000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.AreEqual("Test", events[0].Title, "Line with colon at index 0 should be skipped");
    }

    [TestMethod]
    public async Task GetEventsAsync_WithoutDtend_DateTimeStart_DefaultsTo30Minutes()
    {
        // Tests the fallback when DTEND is missing for a DATE-TIME DTSTART
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:no-dtend\r\nSUMMARY:Short\r\nDTSTART:20260424T090000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 24, 9, 30, 0, TimeSpan.Zero), events[0].End,
            "DATE-TIME without DTEND should default to start + 30 minutes");
    }

    [TestMethod]
    public async Task GetEventsAsync_WithInvalidFeedUrl_ReturnsEmpty()
    {
        // Tests the invalid URI fallback path
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "not-a-valid-url");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, SampleIcs),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.IsEmpty(events, "Invalid feed URL should not crash, just return empty");
    }

    [TestMethod]
    public async Task GetEventsAsync_RejectsEventWithEndBeforeOrEqualStart()
    {
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:bad-times\r\nSUMMARY:Invalid\r\nDTSTART:20260424T100000Z\r\nDTEND:20260424T090000Z\r\nEND:VEVENT\r\n" +
                  "END:VCALENDAR\r\n";

        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, ics),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        var from = new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.IsEmpty(events, "Events where end <= start should be rejected");
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesLocation()
    {
        var events = await GetParsedEventsAsync(SampleIcs,
            new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.HasCount(1, events);
        Assert.AreEqual("Room A", events[0].Location);
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesDescription()
    {
        var events = await GetParsedEventsAsync(SampleIcs,
            new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.HasCount(1, events);
        Assert.AreEqual("Project sync", events[0].Description);
    }

    [TestMethod]
    public async Task GetEventsAsync_WithEmptyIcsContent_ReturnsEmpty()
    {
        var events = await GetParsedEventsAsync("",
            new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.IsEmpty(events);
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesPropertyWithSemicolonParameter()
    {
        // DTSTART;TZID=Europe/Berlin:20260424T090000 - TZID must be respected.
        // Europe/Berlin in April 2026 is CEST (UTC+2), so 09:00 local = 07:00 UTC.
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:param-test\r\nSUMMARY:Param\r\n" +
                  "DTSTART;TZID=Europe/Berlin:20260424T090000\r\n" +
                  "DTEND;TZID=Europe/Berlin:20260424T100000\r\n" +
                  "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var events = await GetParsedEventsAsync(ics,
            new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.HasCount(1, events);
        Assert.AreEqual("param-test", events[0].Id);
        // 09:00 CEST = 07:00 UTC
        Assert.AreEqual(new DateTimeOffset(2026, 4, 24, 7, 0, 0, TimeSpan.Zero), events[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 24, 8, 0, 0, TimeSpan.Zero), events[0].End);
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesDtstartWithTzidAmsterdam_ConvertsToUtc()
    {
        // Europe/Amsterdam in May 2026 = CEST (UTC+2): 19:00 local → 17:00 UTC.
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:amsterdam-evening\r\nSUMMARY:Evening Event\r\n" +
                  "DTSTART;TZID=Europe/Amsterdam:20260506T190000\r\n" +
                  "DTEND;TZID=Europe/Amsterdam:20260506T200000\r\n" +
                  "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var events = await GetParsedEventsAsync(ics,
            new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));

        Assert.HasCount(1, events);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 6, 17, 0, 0, TimeSpan.Zero), events[0].Start,
            "19:00 CEST should be stored as 17:00 UTC");
        Assert.AreEqual(new DateTimeOffset(2026, 5, 6, 18, 0, 0, TimeSpan.Zero), events[0].End);
    }

    [TestMethod]
    public async Task GetEventsAsync_ParsesDtstartWithUnknownTzid_FallsBackToFloatingUtc()
    {
        // When TZID is present but not recognised on this host, treat the time as floating (UTC).
        var ics = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                  "BEGIN:VEVENT\r\nUID:unknown-tz\r\nSUMMARY:Unknown TZ\r\n" +
                  "DTSTART;TZID=Fictional/Nowhere:20260424T090000\r\n" +
                  "DTEND;TZID=Fictional/Nowhere:20260424T100000\r\n" +
                  "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var events = await GetParsedEventsAsync(ics,
            new DateTimeOffset(2026, 4, 24, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.HasCount(1, events);
        // Unknown TZID → fallback to floating (UTC)
        Assert.AreEqual(new DateTimeOffset(2026, 4, 24, 9, 0, 0, TimeSpan.Zero), events[0].Start);
    }

    // Helper for parsing-through-DB tests
    private static async Task<IReadOnlyList<Domain.Models.CalendarEvent>> GetParsedEventsAsync(
        string icsContent, DateTimeOffset from, DateTimeOffset to)
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedOwnerWithFeedsAsync(dbContext, "https://calendar.example.test/feed.ics");

        var source = new IcalFeedCalendarSource(
            CreateHttpClient(HttpStatusCode.OK, icsContent),
            dbContext,
            new MockCalendarSource(),
            NullLogger<IcalFeedCalendarSource>.Instance);

        return await source.GetEventsAsync(from, to, ownerId);
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

    private static AppDbContext CreateDbContext() => TestDbContextFactory.CreateInMemory();

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
