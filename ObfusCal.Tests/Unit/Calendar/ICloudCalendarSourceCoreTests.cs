using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class ICloudCalendarSourceCoreTests
{
    [TestMethod]
    public async Task GetEventsAsync_WithFromAfterTo_ThrowsArgumentException()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient();
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = new ICloudCalendarSourceCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

        var from = DateTimeOffset.UtcNow.AddDays(1);
        var to = DateTimeOffset.UtcNow;

        Assert.ThrowsExactly<ArgumentException>((Action)Act);
        return;

        // Act & Assert
        void Act() => core.GetEventsAsync(from, to).GetAwaiter().GetResult();
    }

    [TestMethod]
    public async Task GetEventsAsync_WithNullCalendarOwnerId_ReturnsEmpty()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient();
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = new ICloudCalendarSourceCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

        // Act
        var result = await core.GetEventsAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), calendarOwnerId: null);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetEventsAsync_WithUnknownCalendarOwnerId_ReturnsEmpty()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient();
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = new ICloudCalendarSourceCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

        // Act
        var result = await core.GetEventsAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1),
            calendarOwnerId: Guid.NewGuid());

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetEventsAsync_WithMissingConfiguration_ReturnsEmpty()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient();
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = new ICloudCalendarSourceCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

        // Add owner without iCloud configuration
        var owner = new CalendarOwner { Id = Guid.NewGuid(), Name = "Test Owner" };
        db.CalendarOwners.Add(owner);
        await db.SaveChangesAsync();

        // Act
        var result = await core.GetEventsAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1),
            calendarOwnerId: owner.Id);

        // Assert
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetEventsAsync_WithValidConfiguration_ParsesCalendarData()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.ICloudCalendar.Credentials.v1");

        var owner = new CalendarOwner
        {
            Id = Guid.NewGuid(),
            Name = "Test Owner",
            ICloudCalendarUrl = "https://caldav.icloud.com/123456789/calendar/",
            ICloudAppleIdProtected = protector.Protect("test@example.com"),
            ICloudAppSpecificPasswordProtected = protector.Protect("app-password")
        };
        db.CalendarOwners.Add(owner);
        await db.SaveChangesAsync();

        var httpClient = new HttpClient(new TestCalendarResponseHandler());
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = new ICloudCalendarSourceCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

        const string dateTimeFrom = "2026-05-06T00:00:00Z";
        const string dateTimeTo = "2026-05-06T00:00:00Z";

        // Act
        var result = await core.GetEventsAsync(
            DateTimeOffset.Parse(dateTimeFrom, new CultureInfo("en-US")),
            DateTimeOffset.Parse(dateTimeTo, new CultureInfo("en-US")),
            calendarOwnerId: owner.Id);

        // Assert
        Assert.IsTrue(result.Count > 0);
        Assert.AreEqual("Test Event", result[0].Title);
    }

    [TestMethod]
    public async Task GetReadinessAsync_WithMissingConfiguration_ReturnsNotReady()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient();
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = new ICloudCalendarSourceCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

        var owner = new CalendarOwner { Id = Guid.NewGuid(), Name = "Test Owner" };
        db.CalendarOwners.Add(owner);
        await db.SaveChangesAsync();

        // Act
        var result = await core.GetReadinessAsync(owner.Id);

        // Assert
        Assert.IsFalse(result.IsReady);
    }

    [TestMethod]
    public async Task GetReadinessAsync_WithUnknownCalendarOwner_ReturnsNotReady()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient();
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = new ICloudCalendarSourceCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

        // Act
        var result = await core.GetReadinessAsync(Guid.NewGuid());

        // Assert
        Assert.IsFalse(result.IsReady);
    }

    [TestMethod]
    public async Task GetReadinessAsync_WithSuccessfulProbe_ReturnsReady()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.ICloudCalendar.Credentials.v1");

        var owner = new CalendarOwner
        {
            Id = Guid.NewGuid(),
            Name = "Test Owner",
            ICloudCalendarUrl = "https://caldav.icloud.com/123456789/calendar/",
            ICloudAppleIdProtected = protector.Protect("test@example.com"),
            ICloudAppSpecificPasswordProtected = protector.Protect("app-password")
        };
        db.CalendarOwners.Add(owner);
        await db.SaveChangesAsync();

        var httpClient = new HttpClient(new TestCalendarResponseHandler());
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = new ICloudCalendarSourceCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

        // Act
        var result = await core.GetReadinessAsync(owner.Id);

        // Assert
        Assert.IsTrue(result.IsReady);
    }

    private static ILogger<ICloudCalendarSourceCore> CreateLogger()
        => LoggerFactory.Create(_ => { }).CreateLogger<ICloudCalendarSourceCore>();

    private class TestCalendarResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var calendarXml = """
                <?xml version="1.0" encoding="utf-8"?>
                <d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
                  <d:response>
                    <d:href>/path/event.ics</d:href>
                    <d:propstat>
                      <d:prop>
                        <d:getetag>"etag123"</d:getetag>
                        <c:calendar-data>BEGIN:VCALENDAR
                VERSION:2.0
                PRODID:-//Apple//Apple Calendar//EN
                CALSCALE:GREGORIAN
                BEGIN:VEVENT
                UID:test-event@example.com
                SUMMARY:Test Event
                DTSTART:20260506T100000Z
                DTEND:20260506T110000Z
                END:VEVENT
                END:VCALENDAR</c:calendar-data>
                      </d:prop>
                      <d:status>HTTP/1.1 200 OK</d:status>
                    </d:propstat>
                  </d:response>
                </d:multistatus>
                """;

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(calendarXml, System.Text.Encoding.UTF8, "application/xml")
            };

            return Task.FromResult(response);
        }
    }
}

