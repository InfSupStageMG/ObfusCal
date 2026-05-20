using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;
using BusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class ICloudCalendarSourceCoreWriteBackTests
{

    private static ICloudCalendarSourceCore CreateCore(
        HttpClient httpClient,
        AppDbContext db,
        ILogger<ICloudCalendarSourceCore>? logger = null) =>
        new(
            httpClient,
            db,
            new EphemeralDataProtectionProvider(),
            new PassthroughCalendarSourceSecretProtector(),
            Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 }),
            logger ?? LoggerFactory.Create(_ => { }).CreateLogger<ICloudCalendarSourceCore>());

    private static BusySlot MakeSlot(string slotId, DateTimeOffset start, DateTimeOffset end) =>
        new(slotId, start, end);

    private static string MakeManagedIcs(string uid, string slotId, DateTimeOffset start, DateTimeOffset end,
        string title)
    {
        var s = start.UtcDateTime.ToString("yyyyMMddTHHmmssZ");
        var e = end.UtcDateTime.ToString("yyyyMMddTHHmmssZ");
        return "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
               "BEGIN:VEVENT\r\n" +
               $"UID:{uid}\r\n" +
               $"SUMMARY:{title}\r\n" +
               $"DTSTART:{s}\r\n" +
               $"DTEND:{e}\r\n" +
               "X-OBFUSCAL-MANAGED:TRUE\r\n" +
               $"X-OBFUSCAL-SLOT-ID:{slotId}\r\n" +
               "END:VEVENT\r\n" +
               "END:VCALENDAR";
    }

    private static string BuildCalDavReportResponse(
        IEnumerable<(string Href, string Uid, string SlotId, DateTimeOffset Start, DateTimeOffset End, string Title)>
            events)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append("<d:multistatus xmlns:d=\"DAV:\" xmlns:cal=\"urn:ietf:params:xml:ns:caldav\">");

        foreach (var (href, uid, slotId, start, end, title) in events)
        {
            var ics = MakeManagedIcs(uid, slotId, start, end, title);
            sb.Append($"<d:response><d:href>{href}</d:href>");
            sb.Append("<d:propstat><d:prop>");
            sb.Append($"<cal:calendar-data>{ics}</cal:calendar-data>");
            sb.Append("</d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>");
        }

        sb.Append("</d:multistatus>");
        return sb.ToString();
    }

    private static string BuildEmptyCalDavReportResponse() =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<d:multistatus xmlns:d=\"DAV:\" xmlns:cal=\"urn:ietf:params:xml:ns:caldav\"></d:multistatus>";

    private static CalendarOwner SeedOwnerWithICloudConfig(AppDbContext db, Guid ownerId,
        string calendarUrl = "https://caldav.icloud.com/user/calendar/")
    {
        var owner = new CalendarOwner
        {
            Id = ownerId,
            Name = "Test Owner",
            ICloudCalendarUrl = calendarUrl,
            ICloudAppleIdProtected = "user@example.com",
            ICloudAppSpecificPasswordProtected = "app-password"
        };
        db.CalendarOwners.Add(owner);
        db.SaveChanges();
        return owner;
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_WithUnknownOwner_DoesNotMakeHttpRequests()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var httpCalled = false;
        var handler = new StubHttpMessageHandler(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var core = CreateCore(new HttpClient(handler), db);

        await core.WriteBackSlotsAsync(Guid.NewGuid(), [], "Busy", DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1));

        Assert.IsFalse(httpCalled);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_WithMissingICloudConfiguration_DoesNotMakeHttpRequests()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Owner" });
        await db.SaveChangesAsync();

        var httpCalled = false;
        var handler = new StubHttpMessageHandler(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var core = CreateCore(new HttpClient(handler), db);

        await core.WriteBackSlotsAsync(ownerId, [], "Busy", DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1));

        Assert.IsFalse(httpCalled);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_CreatesPlaceholderEventsViaCalDavPut()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        SeedOwnerWithICloudConfig(db, ownerId);

        var slotId = "slot-001";
        var start = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);
        var slot = MakeSlot(slotId, start, end);

        var putRequests = new List<HttpRequestMessage>();

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Put)
                putRequests.Add(request);

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(BuildEmptyCalDavReportResponse(), Encoding.UTF8, "application/xml")
            };
        });

        var core = CreateCore(new HttpClient(handler), db);

        await core.WriteBackSlotsAsync(ownerId, [slot], "Busy", start.AddHours(-1), end.AddHours(1));

        Assert.HasCount(1, putRequests);
        var putUri = putRequests[0].RequestUri!.ToString();
        var uid = ICloudCalendarSourceCore.GetManagedEventUid(slotId);
        Assert.Contains(uid + ".ics", putUri);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_PutRequestContainsManagedMarkerAndSlotId()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        SeedOwnerWithICloudConfig(db, ownerId);

        var slotId = "slot-abc";
        var start = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);
        var slot = MakeSlot(slotId, start, end);

        string? capturedBody = null;

        var handler = new StubHttpMessageHandler(async request =>
        {
            if (request.Method == HttpMethod.Put && request.Content is not null)
                capturedBody = await request.Content.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(BuildEmptyCalDavReportResponse(), Encoding.UTF8, "application/xml")
            };
        });

        var core = CreateCore(new HttpClient(handler), db);

        await core.WriteBackSlotsAsync(ownerId, [slot], "Obfuscated Busy", start.AddHours(-1), end.AddHours(1));

        Assert.IsNotNull(capturedBody);
        Assert.Contains("X-OBFUSCAL-MANAGED:TRUE", capturedBody);
        Assert.Contains($"X-OBFUSCAL-SLOT-ID:{slotId}", capturedBody);
        Assert.Contains("SUMMARY:Obfuscated Busy", capturedBody);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_WritesCorrectStartAndEnd()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        SeedOwnerWithICloudConfig(db, ownerId);

        var start = new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var end = start.AddHours(2);
        var slot = MakeSlot("s1", start, end);

        string? capturedBody = null;

        var handler = new StubHttpMessageHandler(async request =>
        {
            if (request.Method == HttpMethod.Put && request.Content is not null)
                capturedBody = await request.Content.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(BuildEmptyCalDavReportResponse(), Encoding.UTF8, "application/xml")
            };
        });

        var core = CreateCore(new HttpClient(handler), db);
        await core.WriteBackSlotsAsync(ownerId, [slot], "Busy", start.AddHours(-1), end.AddHours(1));

        Assert.IsNotNull(capturedBody);
        Assert.Contains("DTSTART:20260615T103000Z", capturedBody);
        Assert.Contains("DTEND:20260615T123000Z", capturedBody);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_SkipsPut_WhenSlotIsAlreadyUpToDate()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        SeedOwnerWithICloudConfig(db, ownerId);

        var slotId = "slot-123";
        var start = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);
        var slot = MakeSlot(slotId, start, end);
        const string title = "Busy Placeholder";

        var uid = ICloudCalendarSourceCore.GetManagedEventUid(slotId);
        var href = $"/user/calendar/{uid}.ics";

        var reportXml = BuildCalDavReportResponse([(href, uid, slotId, start, end, title)]);

        var putCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Put) putCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reportXml, Encoding.UTF8, "application/xml")
            };
        });

        var core = CreateCore(new HttpClient(handler), db);
        await core.WriteBackSlotsAsync(ownerId, [slot], title, start.AddHours(-1), end.AddHours(1));

        Assert.AreEqual(0, putCount, "PUT should be skipped when the existing event is already up-to-date.");
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_DeletesStaleManagedEvents()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        SeedOwnerWithICloudConfig(db, ownerId);

        var windowStart = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddDays(7);

        var staleSlotId = "stale-slot";
        var staleUid = ICloudCalendarSourceCore.GetManagedEventUid(staleSlotId);
        var staleHref = $"/user/calendar/{staleUid}.ics";
        var staleStart = windowStart.AddHours(2);
        var staleEnd = staleStart.AddHours(1);

        var reportXml = BuildCalDavReportResponse([(staleHref, staleUid, staleSlotId, staleStart, staleEnd, "Old Busy")]);

        var deleteUris = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Delete)
                deleteUris.Add(request.RequestUri!.ToString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reportXml, Encoding.UTF8, "application/xml")
            };
        });

        var core = CreateCore(new HttpClient(handler), db);

        await core.WriteBackSlotsAsync(ownerId, [], "Busy", windowStart, windowEnd);

        Assert.HasCount(1, deleteUris);
        Assert.Contains(staleUid, deleteUris[0]);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_KeepsManagedEventWhenSlotIsStillActive()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        SeedOwnerWithICloudConfig(db, ownerId);

        var slotId = "active-slot";
        var uid = ICloudCalendarSourceCore.GetManagedEventUid(slotId);
        var href = $"/user/calendar/{uid}.ics";
        var start = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);
        const string title = "Busy";

        var reportXml = BuildCalDavReportResponse([(href, uid, slotId, start, end, title)]);

        var deleteCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Delete) deleteCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reportXml, Encoding.UTF8, "application/xml")
            };
        });

        var core = CreateCore(new HttpClient(handler), db);

        await core.WriteBackSlotsAsync(ownerId, [MakeSlot(slotId, start, end)], title, start.AddHours(-1),
            end.AddHours(1));

        Assert.AreEqual(0, deleteCount, "Active slots must not be deleted.");
    }

    [TestMethod]
    public void ParseManagedCalDavEvents_EmptyBody_ReturnsEmpty()
    {
        var result = ICloudCalendarSourceCore.ParseManagedCalDavEvents(string.Empty);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void ParseManagedCalDavEvents_EventWithoutManagedMarker_IsExcluded()
    {
        var nativeIcs = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                        "BEGIN:VEVENT\r\nUID:native-event\r\nSUMMARY:My Meeting\r\n" +
                        "DTSTART:20260610T090000Z\r\nDTEND:20260610T100000Z\r\n" +
                        "END:VEVENT\r\nEND:VCALENDAR";

        var xml = "<?xml version=\"1.0\"?><d:multistatus xmlns:d=\"DAV:\" xmlns:cal=\"urn:ietf:params:xml:ns:caldav\">" +
                  "<d:response><d:href>/calendar/event1.ics</d:href>" +
                  "<d:propstat><d:prop>" +
                  $"<cal:calendar-data>{nativeIcs}</cal:calendar-data>" +
                  "</d:prop></d:propstat></d:response></d:multistatus>";

        var result = ICloudCalendarSourceCore.ParseManagedCalDavEvents(xml);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void ParseManagedCalDavEvents_ManagedEvent_IsIncludedWithCorrectSlotId()
    {
        var slotId = "peer-slot-xyz";
        var uid = ICloudCalendarSourceCore.GetManagedEventUid(slotId);
        var href = $"/calendar/{uid}.ics";
        var start = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);

        var ics = MakeManagedIcs(uid, slotId, start, end, "Busy");
        var xml = "<?xml version=\"1.0\"?><d:multistatus xmlns:d=\"DAV:\" xmlns:cal=\"urn:ietf:params:xml:ns:caldav\">" +
                  $"<d:response><d:href>{href}</d:href>" +
                  "<d:propstat><d:prop>" +
                  $"<cal:calendar-data>{ics}</cal:calendar-data>" +
                  "</d:prop></d:propstat></d:response></d:multistatus>";

        var result = ICloudCalendarSourceCore.ParseManagedCalDavEvents(xml);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(href, result[0].Href);
        Assert.AreEqual(slotId, result[0].SlotId);
        Assert.AreEqual("Busy", result[0].Summary);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero), result[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero), result[0].End);
    }

    [TestMethod]
    public void ParseManagedCalDavEvents_MixedEvents_FiltersOutNonManaged()
    {
        var slotId = "managed-slot";
        var uid = ICloudCalendarSourceCore.GetManagedEventUid(slotId);
        var start = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);

        var nativeIcs = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
                        "BEGIN:VEVENT\r\nUID:native\r\nSUMMARY:Native Event\r\n" +
                        "DTSTART:20260610T080000Z\r\nDTEND:20260610T090000Z\r\n" +
                        "END:VEVENT\r\nEND:VCALENDAR";

        var managedIcs = MakeManagedIcs(uid, slotId, start, start.AddHours(1), "Busy");

        var xml = "<?xml version=\"1.0\"?><d:multistatus xmlns:d=\"DAV:\" xmlns:cal=\"urn:ietf:params:xml:ns:caldav\">" +
                  "<d:response><d:href>/calendar/native.ics</d:href>" +
                  "<d:propstat><d:prop>" +
                  $"<cal:calendar-data>{nativeIcs}</cal:calendar-data>" +
                  "</d:prop></d:propstat></d:response>" +
                  $"<d:response><d:href>/calendar/{uid}.ics</d:href>" +
                  "<d:propstat><d:prop>" +
                  $"<cal:calendar-data>{managedIcs}</cal:calendar-data>" +
                  "</d:prop></d:propstat></d:response>" +
                  "</d:multistatus>";

        var result = ICloudCalendarSourceCore.ParseManagedCalDavEvents(xml);

        Assert.HasCount(1, result);
        Assert.AreEqual(slotId, result[0].SlotId);
    }

    [TestMethod]
    public void BuildPlaceholderIcsContent_ContainsRequiredICalFields()
    {
        var slotId = "s42";
        var uid = ICloudCalendarSourceCore.GetManagedEventUid(slotId);
        var start = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);
        var slot = MakeSlot(slotId, start, end);

        var ics = ICloudCalendarSourceCore.BuildPlaceholderIcsContent(uid, slot, "Conference Block");

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("END:VCALENDAR", ics);
        Assert.Contains("BEGIN:VEVENT", ics);
        Assert.Contains("END:VEVENT", ics);
        Assert.Contains($"UID:{uid}", ics);
        Assert.Contains("SUMMARY:Conference Block", ics);
        Assert.Contains("DTSTART:20260610T090000Z", ics);
        Assert.Contains("DTEND:20260610T100000Z", ics);
        Assert.Contains("X-OBFUSCAL-MANAGED:TRUE", ics);
        Assert.Contains($"X-OBFUSCAL-SLOT-ID:{slotId}", ics);
    }

    [TestMethod]
    public void BuildPlaceholderIcsContent_UsesCrlfLineEndings()
    {
        var uid = ICloudCalendarSourceCore.GetManagedEventUid("slot");
        var slot = MakeSlot("slot",
            new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero));

        var ics = ICloudCalendarSourceCore.BuildPlaceholderIcsContent(uid, slot, "Busy");

        Assert.Contains("\r\n", ics, "iCal content must use CRLF line endings.");
    }

    [TestMethod]
    public void GetManagedEventUid_SameSlotIdProducesSameUid()
    {
        var uid1 = ICloudCalendarSourceCore.GetManagedEventUid("abc");
        var uid2 = ICloudCalendarSourceCore.GetManagedEventUid("abc");
        Assert.AreEqual(uid1, uid2);
    }

    [TestMethod]
    public void GetManagedEventUid_DifferentSlotIdsProduceDifferentUids()
    {
        var uid1 = ICloudCalendarSourceCore.GetManagedEventUid("slot-a");
        var uid2 = ICloudCalendarSourceCore.GetManagedEventUid("slot-b");
        Assert.AreNotEqual(uid1, uid2);
    }

    [TestMethod]
    public void GetManagedEventUid_StartsWithObfusCPrefix()
    {
        var uid = ICloudCalendarSourceCore.GetManagedEventUid("any-slot");
        Assert.IsTrue(uid.StartsWith("obfuscal-", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_InstanceBased_WithMissingConfig_DoesNotMakeHttpRequests()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Owner" });
        await db.SaveChangesAsync();

        var httpCalled = false;
        var handler = new StubHttpMessageHandler(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var core = CreateCore(new HttpClient(handler), db);

        var instance = new CalendarSourceInstanceContext(
            Guid.NewGuid(), ownerId, "icloud", "iCloud Calendar",
            true, null, null, false);

        await core.WriteBackSlotsAsync(instance, [], "Busy", DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1));

        Assert.IsFalse(httpCalled);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            : this(request => Task.FromResult(handler(request)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handler(request);
    }

    private sealed class PassthroughCalendarSourceSecretProtector : ICalendarSourceSecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string protectedValue) => protectedValue;
    }
}
