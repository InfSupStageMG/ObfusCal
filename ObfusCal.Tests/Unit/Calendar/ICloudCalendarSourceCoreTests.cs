using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Interfaces;
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
        var result = await core.GetEventsAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1),
            calendarOwnerId: null);

        // Assert
        Assert.IsEmpty(result);
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

    [TestMethod]
    public async Task GetReadinessAsync_WithLegacyPlaintextInstanceSecrets_ReturnsReady()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        }));
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = CreateCore(
            httpClient,
            db,
            dataProtectionProvider,
            icloudOptions,
            logger,
            new PrefixCalendarSourceSecretProtector("enc:"));

        var instance = new CalendarSourceInstanceContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "icloud",
            "iCloud Calendar",
            true,
            "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            "{\"appleId\":\"user@example.com\",\"appSpecificPassword\":\"app-password\"}",
            false);

        var readiness = await core.GetReadinessAsync(instance, CancellationToken.None);

        Assert.IsTrue(readiness.IsReady);
    }

    [TestMethod]
    public async Task GetEventsAsync_WithLegacyPlaintextInstanceSecrets_UsesFallbackAndReturnsEvents()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\">"
                + "<d:response><d:propstat><d:prop><c:calendar-data>"
                + "BEGIN:VCALENDAR\r\n"
                + "BEGIN:VEVENT\r\n"
                + "UID:test-1\r\n"
                + "DTSTAMP:20260506T000000Z\r\n"
                + "DTSTART:20260506T100000Z\r\n"
                + "DTEND:20260506T110000Z\r\n"
                + "SUMMARY:Test Event\r\n"
                + "END:VEVENT\r\n"
                + "END:VCALENDAR"
                + "</c:calendar-data></d:prop></d:propstat></d:response>"
                + "</d:multistatus>")
        }));
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = CreateCore(
            httpClient,
            db,
            dataProtectionProvider,
            icloudOptions,
            logger,
            new PrefixCalendarSourceSecretProtector("enc:"));

        var instance = new CalendarSourceInstanceContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "icloud",
            "iCloud Calendar",
            true,
            "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            "{\"appleId\":\"user@example.com\",\"appSpecificPassword\":\"app-password\"}",
            false);

        var events = await core.GetEventsAsync(
            instance,
            new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("Test Event", events[0].Title);
    }

    [TestMethod]
    public async Task GetReadinessAsync_WithLegacyPlaintextInstanceSecrets_AutoMigratesToProtectedSecretJson()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        }));
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();
        var protector = new PrefixCalendarSourceSecretProtector("enc:");

        var core = CreateCore(httpClient, db, dataProtectionProvider, icloudOptions, logger, protector);

        var ownerId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();

        db.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            CalendarSourcePluginId = "icloud"
        });

        db.CalendarSourceInstances.Add(new CalendarSourceInstance
        {
            Id = instanceId,
            CalendarOwnerId = ownerId,
            PluginId = "icloud",
            DisplayName = "iCloud",
            IsEnabled = true,
            ConfigurationJson = "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            SecretDataJson = "{\"appleId\":\"user@example.com\",\"appSpecificPassword\":\"app-password\"}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var instance = new CalendarSourceInstanceContext(
            instanceId,
            ownerId,
            "icloud",
            "iCloud Calendar",
            true,
            "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            "{\"appleId\":\"user@example.com\",\"appSpecificPassword\":\"app-password\"}",
            false);

        var readiness = await core.GetReadinessAsync(instance, CancellationToken.None);

        Assert.IsTrue(readiness.IsReady);

        var persisted = await db.CalendarSourceInstances.SingleAsync(x => x.Id == instanceId);
        Assert.IsNotNull(persisted.SecretDataJson);

        // Migration should produce a whole-blob encrypted value (not JSON with individually encrypted fields).
        // With PrefixCalendarSourceSecretProtector("enc:"), the result is: enc:{"appleId":"user@example.com",...}
        Assert.StartsWith("enc:", persisted.SecretDataJson,
            "Migrated secret should be a whole-blob encrypted value.");
        var decrypted = protector.Unprotect(persisted.SecretDataJson);
        Assert.Contains("user@example.com", decrypted);
        Assert.Contains("app-password", decrypted);
    }

    [TestMethod]
    public async Task GetEventsAsync_AfterAutoMigration_StillReturnsEvents()
    {
        // Verifies the round-trip: migrate from plaintext → re-read in next cycle → events returned.
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = new PrefixCalendarSourceSecretProtector("enc:");

        var requestCount = 0;
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\">"
                    + "<d:response><d:propstat><d:prop><c:calendar-data>"
                    + "BEGIN:VCALENDAR\r\n"
                    + "BEGIN:VEVENT\r\n"
                    + "UID:roundtrip-1\r\n"
                    + "DTSTAMP:20260506T000000Z\r\n"
                    + "DTSTART:20260506T100000Z\r\n"
                    + "DTEND:20260506T110000Z\r\n"
                    + "SUMMARY:Round Trip Event\r\n"
                    + "END:VEVENT\r\n"
                    + "END:VCALENDAR"
                    + "</c:calendar-data></d:prop></d:propstat></d:response>"
                    + "</d:multistatus>")
            };
        }));
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();

        var core = CreateCore(httpClient, db, dataProtectionProvider, icloudOptions, logger, protector);

        var ownerId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();

        db.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            CalendarSourcePluginId = "icloud"
        });

        // Store with plaintext secrets (legacy format).
        db.CalendarSourceInstances.Add(new CalendarSourceInstance
        {
            Id = instanceId,
            CalendarOwnerId = ownerId,
            PluginId = "icloud",
            DisplayName = "iCloud",
            IsEnabled = true,
            ConfigurationJson = "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            SecretDataJson = "{\"appleId\":\"user@example.com\",\"appSpecificPassword\":\"app-password\"}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // First call: plaintext fallback → auto-migrate.
        var instance1 = new CalendarSourceInstanceContext(
            instanceId, ownerId, "icloud", "iCloud Calendar", true,
            "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            "{\"appleId\":\"user@example.com\",\"appSpecificPassword\":\"app-password\"}",
            false);

        var events1 = await core.GetEventsAsync(instance1,
            new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(1, events1.Count, "First call (plaintext fallback) should return events.");

        // Simulate second call after migration: read migrated data from DB.
        var persistedInstance = await db.CalendarSourceInstances.AsNoTracking()
            .SingleAsync(x => x.Id == instanceId);
        var decryptedSecretJson = protector.Unprotect(persistedInstance.SecretDataJson!);

        var instance2 = new CalendarSourceInstanceContext(
            instanceId, ownerId, "icloud", "iCloud Calendar", true,
            persistedInstance.ConfigurationJson,
            decryptedSecretJson,
            false);

        var events2 = await core.GetEventsAsync(instance2,
            new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(1, events2.Count, "Second call (after migration) should still return events.");
        Assert.AreEqual("Round Trip Event", events2[0].Title);
    }

    [TestMethod]
    public async Task GetReadinessAsync_WithOwnerProtectedInstanceSecrets_AutoMigratesToInstanceSecretProtector()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var ownerProtector = dataProtectionProvider.CreateProtector("ObfusCal.ICloudCalendar.Credentials.v1");
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        }));
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();
        var instanceProtector = new PrefixCalendarSourceSecretProtector("enc:");

        var core = CreateCore(httpClient, db, dataProtectionProvider, icloudOptions, logger, instanceProtector);

        var ownerId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var ownerProtectedAppleId = ownerProtector.Protect("user@example.com");
        var ownerProtectedPassword = ownerProtector.Protect("app-password");

        db.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            CalendarSourcePluginId = "icloud"
        });

        db.CalendarSourceInstances.Add(new CalendarSourceInstance
        {
            Id = instanceId,
            CalendarOwnerId = ownerId,
            PluginId = "icloud",
            DisplayName = "iCloud",
            IsEnabled = true,
            ConfigurationJson = "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            SecretDataJson =
                $"{{\"appleId\":\"{ownerProtectedAppleId}\",\"appSpecificPassword\":\"{ownerProtectedPassword}\"}}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var instance = new CalendarSourceInstanceContext(
            instanceId,
            ownerId,
            "icloud",
            "iCloud Calendar",
            true,
            "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            $"{{\"appleId\":\"{ownerProtectedAppleId}\",\"appSpecificPassword\":\"{ownerProtectedPassword}\"}}",
            false);

        var readiness = await core.GetReadinessAsync(instance, CancellationToken.None);

        Assert.IsTrue(readiness.IsReady);

        var persisted = await db.CalendarSourceInstances.SingleAsync(x => x.Id == instanceId);
        Assert.IsNotNull(persisted.SecretDataJson);

        // Migration should produce a whole-blob encrypted value.
        Assert.StartsWith("enc:", persisted.SecretDataJson,
            "Migrated secret should be a whole-blob encrypted value.");
        var decrypted = instanceProtector.Unprotect(persisted.SecretDataJson);
        Assert.Contains("user@example.com", decrypted);
        Assert.Contains("app-password", decrypted);
    }

    [TestMethod]
    public async Task GetReadinessAsync_WithProtectedBlobAndPlaintextContext_DoesNotRemigrate()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        }));
        var icloudOptions = Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 });
        var logger = CreateLogger();
        var protector = new PrefixCalendarSourceSecretProtector("enc:");

        var core = CreateCore(httpClient, db, dataProtectionProvider, icloudOptions, logger, protector);

        var ownerId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var originalUpdatedAt = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var plaintextSecretJson = "{\"appleId\":\"user@example.com\",\"appSpecificPassword\":\"app-password\"}";
        var protectedSecretBlob = protector.Protect(plaintextSecretJson);

        db.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            CalendarSourcePluginId = "icloud"
        });

        db.CalendarSourceInstances.Add(new CalendarSourceInstance
        {
            Id = instanceId,
            CalendarOwnerId = ownerId,
            PluginId = "icloud",
            DisplayName = "iCloud",
            IsEnabled = true,
            ConfigurationJson = "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            SecretDataJson = protectedSecretBlob,
            CreatedAtUtc = originalUpdatedAt,
            UpdatedAtUtc = originalUpdatedAt
        });
        await db.SaveChangesAsync();

        // Simulate CalendarSourceInstanceService output: decrypted secret JSON in context.
        var instance = new CalendarSourceInstanceContext(
            instanceId,
            ownerId,
            "icloud",
            "iCloud Calendar",
            true,
            "{\"calendarUrl\":\"https://caldav.icloud.com/test/calendar/\"}",
            plaintextSecretJson,
            false);

        var readiness = await core.GetReadinessAsync(instance, CancellationToken.None);

        Assert.IsTrue(readiness.IsReady);

        var persisted = await db.CalendarSourceInstances.AsNoTracking().SingleAsync(x => x.Id == instanceId);
        Assert.AreEqual(protectedSecretBlob, persisted.SecretDataJson,
            "Protected secret blob should not be rewritten when storage is already protected.");
        Assert.AreEqual(originalUpdatedAt, persisted.UpdatedAtUtc,
            "UpdatedAtUtc should remain unchanged when no migration is needed.");
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
        ILogger<ICloudCalendarSourceCore> logger,
        ICalendarSourceSecretProtector? secretProtector = null)
        => new(
            httpClient,
            db,
            dataProtectionProvider,
            secretProtector ?? new PassthroughCalendarSourceSecretProtector(),
            icloudOptions,
            logger);

    private static ILogger<ICloudCalendarSourceCore> CreateLogger()
        => LoggerFactory.Create(_ => { }).CreateLogger<ICloudCalendarSourceCore>();

    private sealed class PassthroughCalendarSourceSecretProtector : ICalendarSourceSecretProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string Unprotect(string protectedValue) => protectedValue;
    }

    private sealed class PrefixCalendarSourceSecretProtector(string prefix) : ICalendarSourceSecretProtector
    {
        public string Protect(string plaintext) => $"{prefix}{plaintext}";

        public string Unprotect(string protectedValue)
        {
            return !protectedValue.StartsWith(prefix, StringComparison.Ordinal) ? throw new CryptographicException("Value is not protected.") : protectedValue[prefix.Length..];
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
