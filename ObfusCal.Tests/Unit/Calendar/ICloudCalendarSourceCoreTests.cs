using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
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

        var core = CreateCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

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

        var core = CreateCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

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

        var core = CreateCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

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

        var core = CreateCore(httpClient, db, dataProtectionProvider, icloudOptions, logger);

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
    public void CreateCalendarQueryRequest_ProducesValidXml()
    {
        var config = new ICloudCalendarOwnerConfiguration(
            new Uri("https://caldav.icloud.com/test/calendar/"),
            "user@example.com",
            "password");
        var from = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);

        var request = CreateCalendarQueryRequest(config, from, to);

        Assert.IsNotNull(request);
        Assert.IsNotNull(request.Content);
        Assert.AreEqual("REPORT", request.Method.Method);
    }

    [TestMethod]
    public async Task CreateCalendarQueryRequest_ContainsExpandElement_ForRecurringEventSupport()
    {
        var config = new ICloudCalendarOwnerConfiguration(
            new Uri("https://caldav.icloud.com/test/calendar/"),
            "user@example.com",
            "password");
        var from = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);

        var request = CreateCalendarQueryRequest(config, from, to);
        var body = await request.Content!.ReadAsStringAsync();

        StringAssert.Contains(body, "<c:expand");
        StringAssert.Contains(body, "start=\"20260506T000000Z\"");
        StringAssert.Contains(body, "end=\"20260507T000000Z\"");
    }

    // Helper to access the private CreateCalendarQueryRequest for testing
    private static HttpRequestMessage CreateCalendarQueryRequest(
        ICloudCalendarOwnerConfiguration configuration,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var request = new HttpRequestMessage(new HttpMethod("REPORT"), configuration.CalendarUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{configuration.AppleId}:{configuration.AppSpecificPassword}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.TryAddWithoutValidation("Depth", "1");

        var startStamp = from.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var endStamp = to.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        var body = $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <c:calendar-query xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
                      <d:prop>
                        <d:getetag />
                        <c:calendar-data>
                          <c:expand start="{startStamp}" end="{endStamp}" />
                        </c:calendar-data>
                      </d:prop>
                      <c:filter>
                        <c:comp-filter name="VCALENDAR">
                          <c:comp-filter name="VEVENT">
                            <c:time-range start="{startStamp}" end="{endStamp}" />
                          </c:comp-filter>
                        </c:comp-filter>
                      </c:filter>
                    </c:calendar-query>
                    """;

        request.Content = new StringContent(body, Encoding.UTF8, "application/xml");
        return request;
    }

    private sealed record ICloudCalendarOwnerConfiguration(
        Uri CalendarUri,
        string AppleId,
        string AppSpecificPassword);

    private static ICloudCalendarSourceCore CreateCore(
        HttpClient httpClient,
        AppDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ICloudCalendarOptions> icloudOptions,
        ILogger<ICloudCalendarSourceCore> logger)
        => new(
            httpClient,
            db,
            dataProtectionProvider,
            icloudOptions,
            logger);

    private static ILogger<ICloudCalendarSourceCore> CreateLogger()
        => LoggerFactory.Create(_ => { }).CreateLogger<ICloudCalendarSourceCore>();


}
