using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ObfusCal.Application;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Sync;
using BusySlot = ObfusCal.Domain.Models.BusySlot;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Security;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Sync;

[TestClass]
public class CalendarOwnerAvailabilitySyncServiceTests
{
    [TestMethod]
    public async Task RunSyncCycleAsync_ProcessesAllCalendarOwnersAndStoresAvailabilitySnapshots()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerA = new CalendarOwner { Id = Guid.NewGuid(), Name = "Owner A" };
        var ownerB = new CalendarOwner { Id = Guid.NewGuid(), Name = "Owner B" };
        dbContext.CalendarOwners.AddRange(ownerA, ownerB);
        await dbContext.SaveChangesAsync();

        var calendarSource = new StubCalendarSource(new Dictionary<Guid, IReadOnlyList<CalendarEvent>>
        {
            [ownerA.Id] =
            [
                new CalendarEvent(
                    "owner-a-1",
                    "Sensitive A",
                    "Description A",
                    new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
                    ["a@example.test"],
                    "Room A")
            ],
            [ownerB.Id] =
            [
                new CalendarEvent(
                    "owner-b-1",
                    "Sensitive B",
                    "Description B",
                    new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 5, 2, 9, 30, 0, TimeSpan.Zero),
                    ["b@example.test"],
                    "Room B")
            ]
        });

        var logger = new CapturingLogger<CalendarOwnerAvailabilitySyncService>();
        var service = CreateService(dbContext, calendarSource, logger);

        await service.RunSyncCycleAsync();

        var snapshots = await dbContext.CalendarOwnerAvailabilitySlots
            .OrderBy(slot => slot.SourceEventId)
            .ToListAsync();

        Assert.HasCount(2, snapshots);
        Assert.AreEqual(ownerA.Id, snapshots[0].CalendarOwnerId);
        Assert.AreEqual(ownerB.Id, snapshots[1].CalendarOwnerId);
        var owners = await dbContext.CalendarOwners.ToListAsync();
        Assert.IsTrue(owners.All(owner => owner is { LastSyncedAt: not null, LastSyncSucceeded: true }));
        Assert.AreEqual(2, logger.Entries.Count(entry =>
            entry.LogLevel == LogLevel.Information
            && entry.Message.Contains("Availability sync succeeded", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_OnOwnerFailure_ContinuesWithNextOwnerAndRecordsFailureMetadata()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var failingOwner = new CalendarOwner { Id = Guid.NewGuid(), Name = "Failing" };
        var healthyOwner = new CalendarOwner { Id = Guid.NewGuid(), Name = "Healthy" };
        dbContext.CalendarOwners.AddRange(failingOwner, healthyOwner);
        await dbContext.SaveChangesAsync();

        var calendarSource = new ThrowingStubCalendarSource(
            failingOwner.Id,
            [
                new CalendarEvent(
                    "owner-b-1",
                    "Sensitive",
                    "Description",
                    new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 5, 2, 9, 30, 0, TimeSpan.Zero),
                    ["b@example.test"],
                    "Room B")
            ]);
        var logger = new CapturingLogger<CalendarOwnerAvailabilitySyncService>();
        var service = CreateService(dbContext, calendarSource, logger);

        await service.RunSyncCycleAsync();

        var failingOwnerState = await dbContext.CalendarOwners.SingleAsync(owner => owner.Id == failingOwner.Id);
        var healthyOwnerState = await dbContext.CalendarOwners.SingleAsync(owner => owner.Id == healthyOwner.Id);
        var healthySnapshots = await dbContext.CalendarOwnerAvailabilitySlots
            .Where(slot => slot.CalendarOwnerId == healthyOwner.Id)
            .ToListAsync();

        Assert.IsFalse(failingOwnerState.LastSyncSucceeded ?? true);
        Assert.IsNotNull(failingOwnerState.LastSyncedAt);
        Assert.IsTrue(healthyOwnerState.LastSyncSucceeded ?? false);
        Assert.IsNotNull(healthyOwnerState.LastSyncedAt);
        Assert.HasCount(1, healthySnapshots);
        Assert.Contains(entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("Availability sync failed", StringComparison.Ordinal), logger.Entries);
    }

    [TestMethod]
    public async Task RunSyncForOwnerAsync_WithWriteBackEnabled_WritesManagedGraphPlaceholderUsingConfiguredTitle()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");
        var now = DateTimeOffset.UtcNow;

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Graph owner",
            WriteBackEnabled = true,
            WriteBackPlaceholderTitle = "Niet beschikbaar",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = now.AddHours(1)
        });
        await dbContext.SaveChangesAsync();

        var shadowSlot = new BusySlot("peer-slot-1", now.AddMinutes(15), now.AddMinutes(75));
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var source = CreateGraphSource(
            dbContext,
            dataProtectionProvider,
            async request =>
            {
                var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                requests.Add((request.Method, request.RequestUri!.ToString(), body));

                if (request.RequestUri!.AbsolutePath.EndsWith("/me/calendarView", StringComparison.Ordinal) || request.Method == HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"id\":\"managed-1\"}", Encoding.UTF8, "application/json")
                };
            });

        var service = CreateService(
            dbContext,
            source,
            new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
            new StubShadowSlotStore([shadowSlot]));

        await service.RunSyncForOwnerAsync(ownerId);

        var post = requests.Single(entry => entry.Method == HttpMethod.Post);
        using var doc = JsonDocument.Parse(post.Body!);
        Assert.AreEqual("Niet beschikbaar", doc.RootElement.GetProperty("subject").GetString());
        Assert.IsFalse(doc.RootElement.TryGetProperty("attendees", out _));
        Assert.IsFalse(doc.RootElement.TryGetProperty("location", out _));
        Assert.IsFalse(doc.RootElement.TryGetProperty("body", out _));
    }

    [TestMethod]
    public async Task RunSyncForOwnerAsync_WithWriteBackEnabled_DeletesStaleManagedGraphPlaceholder()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");
        var staleStart = DateTimeOffset.UtcNow.AddMinutes(30);

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Graph owner",
            WriteBackEnabled = true,
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        });
        await dbContext.SaveChangesAsync();

        const string slotIdPropertyId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.SlotId";
        var managedEventsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = "managed-1",
                    subject = "Busy",
                    start = new { dateTime = staleStart.ToString("O"), timeZone = "UTC" },
                    end = new { dateTime = staleStart.AddHours(1).ToString("O"), timeZone = "UTC" },
                    singleValueExtendedProperties = new[]
                    {
                        new { id = slotIdPropertyId, value = "stale-slot" }
                    }
                }
            }
        });

        var requests = new List<(HttpMethod Method, string Uri)>();
        var responsesToDispose = new List<HttpResponseMessage>();
        try
        {
            var source = CreateGraphSource(
                dbContext,
                dataProtectionProvider,
                request =>
                {
                    requests.Add((request.Method, request.RequestUri!.ToString()));

                    if (request.RequestUri!.AbsolutePath.EndsWith("/me/calendarView", StringComparison.Ordinal))
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
                        };
                        responsesToDispose.Add(response);
                        return Task.FromResult(response);
                    }

                    if (request.Method == HttpMethod.Get)
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(managedEventsJson, Encoding.UTF8, "application/json")
                        };
                        responsesToDispose.Add(response);
                        return Task.FromResult(response);
                    }

                    var noContentResponse = new HttpResponseMessage(HttpStatusCode.NoContent);
                    responsesToDispose.Add(noContentResponse);
                    return Task.FromResult(noContentResponse);
                });

            var service = CreateService(
                dbContext,
                source,
                new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
                new StubShadowSlotStore([]));

            await service.RunSyncForOwnerAsync(ownerId);

            Assert.ContainsSingle(entry => entry.Method == HttpMethod.Delete, requests);
            Assert.Contains(entry => entry.Method == HttpMethod.Delete && entry.Uri.Contains("managed-1", StringComparison.Ordinal), requests);
        }
        finally
        {
            foreach (var response in responsesToDispose)
            {
                response.Dispose();
            }
        }
    }

    [TestMethod]
    public async Task RunSyncForOwnerAsync_WithWriteBackDisabled_DoesNotInvokeGraphWriteBack()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Graph owner",
            WriteBackEnabled = false,
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        });
        await dbContext.SaveChangesAsync();

        var requests = new List<(HttpMethod Method, string Path)>();
        static Task<HttpResponseMessage> CreateOkGraphResponse()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }

        var source = CreateGraphSource(
            dbContext,
            dataProtectionProvider,
            request =>
            {
                requests.Add((request.Method, request.RequestUri!.AbsolutePath));
                return CreateOkGraphResponse();
            });

        var service = CreateService(
            dbContext,
            source,
            new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
            new StubShadowSlotStore([new BusySlot("peer-slot-1", DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddMinutes(40))]));

        await service.RunSyncForOwnerAsync(ownerId);

        Assert.HasCount(1, requests, "Only the normal calendar read should run when write-back is disabled.");
        Assert.AreEqual("/v1.0/me/calendarView", requests[0].Path);
    }

    [TestMethod]
    public async Task RunSyncForOwnerAsync_WithWriteBackEnabled_WritesManagedGooglePlaceholderUsingConfiguredTitle()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Google owner",
            WriteBackEnabled = true,
            WriteBackPlaceholderTitle = "Niet beschikbaar"
        });
        await dbContext.SaveChangesAsync();

        var shadowSlot = new BusySlot("peer-slot-1", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddMinutes(75));
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var source = CreateGoogleSource(
            dbContext,
            async request =>
            {
                var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                requests.Add((request.Method, request.RequestUri!.ToString(), body));

                if (request.Method == HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"items\":[]}", Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"id\":\"managed-google-1\"}", Encoding.UTF8, "application/json")
                };
            });

        var service = CreateService(
            dbContext,
            source,
            new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
            new StubShadowSlotStore([shadowSlot]));

        await service.RunSyncForOwnerAsync(ownerId);

        var post = requests.Single(entry => entry.Method == HttpMethod.Post);
        using var doc = JsonDocument.Parse(post.Body!);
        Assert.AreEqual("Niet beschikbaar", doc.RootElement.GetProperty("summary").GetString());
        Assert.IsFalse(doc.RootElement.TryGetProperty("attendees", out _));
        Assert.IsFalse(doc.RootElement.TryGetProperty("location", out _));
        Assert.IsFalse(doc.RootElement.TryGetProperty("description", out _));
    }

    [TestMethod]
    public async Task RunSyncForOwnerAsync_WithWriteBackEnabled_DeletesStaleManagedGooglePlaceholder()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var staleStart = DateTimeOffset.UtcNow.AddMinutes(30);

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Google owner",
            WriteBackEnabled = true
        });
        await dbContext.SaveChangesAsync();

        var managedEventsJson = JsonSerializer.Serialize(new
        {
            items = new[]
            {
                new
                {
                    id = "managed-google-1",
                    summary = "Busy",
                    start = new { dateTime = staleStart.ToString("O") },
                    end = new { dateTime = staleStart.AddHours(1).ToString("O") },
                    extendedProperties = new
                    {
                        @private = new Dictionary<string, string>
                        {
                            ["ObfusCal.Managed"] = "1",
                            ["ObfusCal.SlotId"] = "stale-slot"
                        }
                    }
                }
            }
        });

        var requests = new List<(HttpMethod Method, string Uri)>();
        var responsesToDispose = new List<HttpResponseMessage>();
        try
        {
            var source = CreateGoogleSource(
                dbContext,
                request =>
                {
                    requests.Add((request.Method, request.RequestUri!.ToString()));

                    HttpResponseMessage response;
                    if (request.Method == HttpMethod.Get && request.RequestUri.Query.Contains("privateExtendedProperty", StringComparison.Ordinal))
                    {
                        response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(managedEventsJson, Encoding.UTF8, "application/json")
                        };
                    }
                    else if (request.Method == HttpMethod.Get)
                    {
                        response = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"items\":[]}", Encoding.UTF8, "application/json")
                        };
                    }
                    else
                    {
                        response = new HttpResponseMessage(HttpStatusCode.NoContent);
                    }

                    responsesToDispose.Add(response);
                    return Task.FromResult(response);
                });

            var service = CreateService(
                dbContext,
                source,
                new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
                new StubShadowSlotStore([]));

            await service.RunSyncForOwnerAsync(ownerId);

            Assert.ContainsSingle(entry => entry.Method == HttpMethod.Delete, requests);
            Assert.Contains(entry => entry.Method == HttpMethod.Delete && entry.Uri.Contains("managed-google-1", StringComparison.Ordinal), requests);
        }
        finally
        {
            foreach (var response in responsesToDispose)
            {
                response.Dispose();
            }
        }
    }

    [TestMethod]
    public async Task RunSyncForOwnerAsync_WithWriteBackDisabled_DoesNotInvokeGoogleWriteBack()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Google owner",
            WriteBackEnabled = false
        });
        await dbContext.SaveChangesAsync();

        var requests = new List<(HttpMethod Method, string Path)>();
        var source = CreateGoogleSource(
            dbContext,
            request =>
            {
                requests.Add((request.Method, request.RequestUri!.AbsolutePath));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"items\":[]}", Encoding.UTF8, "application/json")
                });
            });

        var service = CreateService(
            dbContext,
            source,
            new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
            new StubShadowSlotStore([new BusySlot("peer-slot-1", DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddMinutes(40))]));

        await service.RunSyncForOwnerAsync(ownerId);

        Assert.HasCount(1, requests, "Only the normal calendar read should run when Google write-back is disabled.");
        Assert.AreEqual("/calendar/v3/calendars/primary/events", requests[0].Path);
    }

    [TestMethod]
    public async Task RunSyncForOwnerAsync_WithWriteBackEnabled_WritesManagedICloudPlaceholderUsingConfiguredTitle()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "iCloud owner",
            WriteBackEnabled = true,
            WriteBackPlaceholderTitle = "Niet beschikbaar",
            ICloudCalendarUrl = "https://caldav.icloud.com/user/calendar/",
            ICloudAppleIdProtected = "user@icloud.com",
            ICloudAppSpecificPasswordProtected = "app-specific-pw"
        });
        await dbContext.SaveChangesAsync();

        var shadowSlot = new BusySlot("peer-slot-1", now.AddMinutes(15), now.AddMinutes(75));
        var putRequests = new List<(string Uri, string Body)>();
        var emptyReportXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<d:multistatus xmlns:d=\"DAV:\" xmlns:cal=\"urn:ietf:params:xml:ns:caldav\"></d:multistatus>";

        var source = CreateICloudSource(dbContext, async request =>
        {
            if (request.Method != HttpMethod.Put || request.Content is null)
                return new HttpResponseMessage(HttpStatusCode.MultiStatus)
                {
                    Content = new StringContent(emptyReportXml, Encoding.UTF8, "application/xml")
                };
            var body = await request.Content.ReadAsStringAsync();
            putRequests.Add((request.RequestUri!.ToString(), body));
            return new HttpResponseMessage(HttpStatusCode.Created);

        });

        var service = CreateService(dbContext, source, new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
            new StubShadowSlotStore([shadowSlot]));

        await service.RunSyncForOwnerAsync(ownerId);

        Assert.HasCount(1, putRequests);
        Assert.Contains("X-OBFUSCAL-MANAGED:TRUE", putRequests[0].Body);
        Assert.Contains("SUMMARY:Niet beschikbaar", putRequests[0].Body);
    }

    [TestMethod]
    public async Task RunSyncForOwnerAsync_WithWriteBackEnabled_DeletesStaleManagedICloudPlaceholder()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var staleStart = DateTimeOffset.UtcNow.AddMinutes(30);
        var staleSlotId = "stale-icloud-slot";
        var staleUid = ICloudCalendarSourceCore.GetManagedEventUid(staleSlotId);

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "iCloud owner",
            WriteBackEnabled = true,
            ICloudCalendarUrl = "https://caldav.icloud.com/user/calendar/",
            ICloudAppleIdProtected = "user@icloud.com",
            ICloudAppSpecificPasswordProtected = "app-specific-pw"
        });
        await dbContext.SaveChangesAsync();

        var staleIcs =
            "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" +
            "BEGIN:VEVENT\r\n" +
            $"UID:{staleUid}\r\n" +
            "SUMMARY:Old Busy\r\n" +
            $"DTSTART:{staleStart.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}\r\n" +
            $"DTEND:{staleStart.AddHours(1).UtcDateTime:yyyyMMdd'T'HHmmss'Z'}\r\n" +
            "X-OBFUSCAL-MANAGED:TRUE\r\n" +
            $"X-OBFUSCAL-SLOT-ID:{staleSlotId}\r\n" +
            "END:VEVENT\r\n" +
            "END:VCALENDAR";

        var reportXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<d:multistatus xmlns:d=\"DAV:\" xmlns:cal=\"urn:ietf:params:xml:ns:caldav\">" +
            $"<d:response><d:href>/user/calendar/{staleUid}.ics</d:href>" +
            "<d:propstat><d:prop>" +
            $"<cal:calendar-data>{staleIcs}</cal:calendar-data>" +
            "</d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>" +
            "</d:multistatus>";

        var deleteUris = new List<string>();
        var source = CreateICloudSource(dbContext, request =>
        {
            if (request.Method != HttpMethod.Delete)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MultiStatus)
                {
                    Content = new StringContent(reportXml, Encoding.UTF8, "application/xml")
                });
            deleteUris.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));

        });

        var service = CreateService(dbContext, source, new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
            new StubShadowSlotStore([]));

        await service.RunSyncForOwnerAsync(ownerId);

        Assert.HasCount(1, deleteUris);
        Assert.Contains(staleUid, deleteUris[0]);
    }

    [TestMethod]
    public async Task RunSyncForOwnerAsync_WithWriteBackDisabled_DoesNotInvokeICloudWriteBack()
    {
        await using var dbContext = SyncIntegrationTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "iCloud owner",
            WriteBackEnabled = false,
            ICloudCalendarUrl = "https://caldav.icloud.com/user/calendar/",
            ICloudAppleIdProtected = "user@icloud.com",
            ICloudAppSpecificPasswordProtected = "app-specific-pw"
        });
        await dbContext.SaveChangesAsync();

        var requestMethods = new List<HttpMethod>();
        var emptyReportXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<d:multistatus xmlns:d=\"DAV:\" xmlns:cal=\"urn:ietf:params:xml:ns:caldav\"></d:multistatus>";

        var source = CreateICloudSource(dbContext, request =>
        {
            requestMethods.Add(request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MultiStatus)
            {
                Content = new StringContent(emptyReportXml, Encoding.UTF8, "application/xml")
            });
        });

        var service = CreateService(dbContext, source, new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
            new StubShadowSlotStore([new BusySlot("peer-slot-1", DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddMinutes(40))]));

        await service.RunSyncForOwnerAsync(ownerId);

        Assert.DoesNotContain(HttpMethod.Put, requestMethods, "PUT must not be issued when write-back is disabled.");
        Assert.DoesNotContain(HttpMethod.Delete, requestMethods, "DELETE must not be issued when write-back is disabled.");
    }

    private static ICalendarSource CreateICloudSource(
        AppDbContext dbContext,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var secretProtector = new CalendarSourceSecretProtector(dataProtectionProvider);
        var messageHandler = new DelegatingHttpMessageHandler(handler);
        var httpClient = new HttpClient(messageHandler, disposeHandler: true);

        var core = new ICloudCalendarSourceCore(
            httpClient,
            dbContext,
            dataProtectionProvider,
            secretProtector,
            Options.Create(new ICloudCalendarOptions { ReadinessProbeLookAheadDays = 1 }),
            NullLogger<ICloudCalendarSourceCore>.Instance);

        return new PerCallICloudCalendarSource(core);
    }

    private static CalendarOwnerAvailabilitySyncService CreateService(
        AppDbContext dbContext,
        ICalendarSource calendarSource,
        CapturingLogger<CalendarOwnerAvailabilitySyncService> logger,
        IShadowSlotStore? shadowSlotStore = null)
    {
        using var applicationServices = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .BuildServiceProvider();

        var scopeProvider = new ServiceCollection()
            .AddSingleton(dbContext)
            .AddSingleton(dbContext)
            .BuildServiceProvider();

        return new CalendarOwnerAvailabilitySyncService(
            dbContext,
            new FixedCalendarSourceResolver(calendarSource),
            applicationServices.GetRequiredService<ObfuscationPipeline>(),
            new StubCalendarOwnerObfuscationProfileService(),
            shadowSlotStore ?? new StubShadowSlotStore(),
            Options.Create(new SyncOptions
            {
                SyncIntervalSeconds = 900,
                LookAheadDays = 14
            }),
            scopeProvider.GetRequiredService<IServiceScopeFactory>(),
            logger);
    }

    private static ICalendarSource CreateGraphSource(
        AppDbContext dbContext,
        IDataProtectionProvider dataProtectionProvider,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var instances = new FakeCalendarSourceInstanceService(ownerId => dbContext.CalendarOwners.Any(owner => owner.Id == ownerId));
        return new PerCallGraphCalendarSource(dbContext, dataProtectionProvider, handler, instances);
    }

    private static ICalendarSource CreateGoogleSource(
        AppDbContext dbContext,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var secretProtector = new CalendarSourceSecretProtector(dataProtectionProvider);
        var instances = new FakeCalendarSourceInstanceService(ownerId => dbContext.CalendarOwners.Any(owner => owner.Id == ownerId));
        return new PerCallGoogleCalendarSource(dbContext, secretProtector, handler, instances);
    }

    private sealed class PerCallGraphCalendarSource(
        AppDbContext dbContext,
        IDataProtectionProvider dataProtectionProvider,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler,
        FakeCalendarSourceInstanceService instances) : ICalendarSource, ICalendarWriteBack
    {
        private async Task<TResult> WithInnerSourceAsync<TResult>(
            Func<GraphCalendarSource, Task<TResult>> action)
        {
            var messageHandler = new DelegatingHttpMessageHandler(handler);
            using var httpClient = new HttpClient(messageHandler, disposeHandler: true);
            httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");

            var source = new GraphCalendarSource(
                httpClient,
                dbContext,
                dataProtectionProvider,
                new StubGraphOAuthTokenClient(),
                instances,
                NullLogger<GraphCalendarSource>.Instance);

            return await action(source);
        }

        public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? calendarOwnerId = null,
            CancellationToken ct = default)
        {
            return await WithInnerSourceAsync(source => source.GetEventsAsync(from, to, calendarOwnerId, ct));
        }

        public async Task WriteBackSlotsAsync(
            Guid calendarOwnerId,
            IReadOnlyList<BusySlot> busySlots,
            string placeholderTitle,
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default)
        {
            await WithInnerSourceAsync(async source =>
            {
                await source.WriteBackSlotsAsync(calendarOwnerId, busySlots, placeholderTitle, windowStart, windowEnd, ct);
                return 0;
            });
        }
    }

    private sealed class PerCallGoogleCalendarSource(
        AppDbContext dbContext,
        ICalendarSourceSecretProtector secretProtector,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler,
        FakeCalendarSourceInstanceService instances) : ICalendarSource, ICalendarWriteBack
    {
        private async Task EnsureGoogleInstanceAsync(Guid calendarOwnerId, CancellationToken ct)
        {
            var existing = await instances.GetFirstAsync(calendarOwnerId, "google", ct);
            if (existing is not null)
                return;

            await instances.CreateAsync(
                calendarOwnerId,
                new CreateCalendarSourceInstanceInput(
                    "google",
                    "Google Calendar",
                    "{\"calendarId\":\"primary\"}",
                    SerializeGoogleSecret(secretProtector, "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1))),
                ct);
        }

        private async Task<TResult> WithInnerSourceAsync<TResult>(
            Guid calendarOwnerId,
            CancellationToken ct,
            Func<GoogleCalendarSourceCore, Task<TResult>> action)
        {
            await EnsureGoogleInstanceAsync(calendarOwnerId, ct);

            var messageHandler = new DelegatingHttpMessageHandler(handler);
            using var httpClient = CreateGoogleHttpClient(messageHandler);

            var source = new GoogleCalendarSourceCore(
                httpClient,
                dbContext,
                instances,
                secretProtector,
                new StubGoogleOAuthTokenClient(),
                Options.Create(new GoogleConsentOptions
                {
                    ApiBaseUrl = "https://www.googleapis.com",
                    AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                    TokenEndpoint = "https://oauth2.googleapis.com/token",
                    Scope = "https://www.googleapis.com/auth/calendar.events"
                }),
                NullLogger<GoogleCalendarSourceCore>.Instance);

            return await action(source);
        }

        private static HttpClient CreateGoogleHttpClient(HttpMessageHandler messageHandler)
        {
            var httpClient = new HttpClient(messageHandler, disposeHandler: true);
            httpClient.BaseAddress = new Uri("https://www.googleapis.com/");
            return httpClient;
        }

        public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? calendarOwnerId = null,
            CancellationToken ct = default)
        {
            if (calendarOwnerId is null)
                return [];

            return await WithInnerSourceAsync(calendarOwnerId.Value, ct, source => source.GetEventsAsync(from, to, calendarOwnerId, ct));
        }

        public async Task WriteBackSlotsAsync(
            Guid calendarOwnerId,
            IReadOnlyList<BusySlot> busySlots,
            string placeholderTitle,
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default)
        {
            await WithInnerSourceAsync(calendarOwnerId, ct, async source =>
            {
                await source.WriteBackSlotsAsync(calendarOwnerId, busySlots, placeholderTitle, windowStart, windowEnd, ct);
                return 0;
            });
        }

        private static string SerializeGoogleSecret(
            ICalendarSourceSecretProtector protector,
            string accessToken,
            string refreshToken,
            DateTimeOffset expiresAtUtc)
            => JsonSerializer.Serialize(new GoogleCalendarSourceCore.GoogleSourceSecretData(
                protector.Protect(accessToken),
                protector.Protect(refreshToken),
                DateTimeOffset.UtcNow,
                expiresAtUtc,
                DateTimeOffset.UtcNow));
    }

    private sealed class StubCalendarSource(IDictionary<Guid, IReadOnlyList<CalendarEvent>> eventsByOwner) : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? calendarOwnerId = null,
            CancellationToken ct = default)
            => Task.FromResult(calendarOwnerId is { } ownerId && eventsByOwner.TryGetValue(ownerId, out var events)
                ? events
                : []);
    }

    private sealed class FixedCalendarSourceResolver(ICalendarSource source) : ICalendarSourceResolver
    {
        public Task<ICalendarSource> ResolveAsync(Guid? calendarOwnerId = null, CancellationToken ct = default) =>
            Task.FromResult(source);
    }

    private sealed class ThrowingStubCalendarSource(Guid failingOwnerId, IReadOnlyList<CalendarEvent> healthyEvents) : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? calendarOwnerId = null,
            CancellationToken ct = default)
        {
            return calendarOwnerId == failingOwnerId ? throw new InvalidOperationException("boom") : Task.FromResult(healthyEvents);
        }
    }

    private sealed class StubCalendarOwnerObfuscationProfileService : ICalendarOwnerObfuscationProfileService
    {
        public Task<IReadOnlyList<ObfuscationProfileSettings>> GetProfilesAsync(Guid calendarOwnerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ObfuscationProfileSettings>>(
                Enum.GetValues<ObfuscationAuditContext>().Select(ObfuscationProfileSettings.CreateDefault).ToList());

        public Task<ObfuscationProfileSettings> GetProfileAsync(Guid calendarOwnerId, ObfuscationAuditContext context, CancellationToken ct = default)
            => Task.FromResult(ObfuscationProfileSettings.CreateDefault(context));

        public Task<ObfuscationProfileSettings> SetProfileAsync(Guid calendarOwnerId, ObfuscationProfileSettings profile, CancellationToken ct = default)
            => Task.FromResult(profile);
    }

    private sealed class StubShadowSlotStore(IReadOnlyList<BusySlot>? allSlots = null) : IShadowSlotStore
    {
        private readonly IReadOnlyList<BusySlot> _allSlots = allSlots ?? [];

        public Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetSlotsAsync(string peerId, Guid calendarOwnerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BusySlot>>([]);
        public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, Guid calendarOwnerId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BusySlot>>([]);
        public Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) => Task.FromResult(_allSlots);
        public Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(Guid calendarOwnerId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) => Task.FromResult(_allSlots);
    }

    private sealed class StubGraphOAuthTokenClient : IGraphOAuthTokenClient
    {
        public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string authorizationCode, string redirectUri, string? scope = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, string? scope = null, CancellationToken ct = default)
            => Task.FromResult(new GraphOAuthTokenResponse("access-token", refreshToken, scope, DateTimeOffset.UtcNow.AddHours(1)));
    }

    private sealed class StubGoogleOAuthTokenClient : IGoogleOAuthTokenClient
    {
        public Task<GoogleOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string authorizationCode, string redirectUri, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<GoogleOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
            => Task.FromResult(new GoogleOAuthTokenResponse("access-token", refreshToken, DateTimeOffset.UtcNow.AddHours(1)));
    }

    private sealed class DelegatingHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }

    private sealed class PerCallICloudCalendarSource(ICloudCalendarSourceCore core)
        : ICalendarSource, ICalendarWriteBack
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? calendarOwnerId = null,
            CancellationToken ct = default)
            => core.GetEventsAsync(from, to, calendarOwnerId, ct);

        public Task WriteBackSlotsAsync(
            Guid calendarOwnerId,
            IReadOnlyList<BusySlot> busySlots,
            string placeholderTitle,
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default)
            => core.WriteBackSlotsAsync(calendarOwnerId, busySlots, placeholderTitle, windowStart, windowEnd, ct);
    }
}
