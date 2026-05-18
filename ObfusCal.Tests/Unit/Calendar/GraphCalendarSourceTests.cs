using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;
using BusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class GraphCalendarSourceTests
{
    [TestMethod]
    public async Task GetEventsAsync_MapsGraphCalendarViewResponse()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            Assert.AreEqual("https://graph.microsoft.com/v1.0/me/calendarView",
                request.RequestUri!.GetLeftPart(UriPartial.Path));
            Assert.AreEqual("Bearer", request.Headers.Authorization?.Scheme);
            Assert.AreEqual("access-token", request.Headers.Authorization?.Parameter);

            var json = """
                       {
                         "value": [
                           {
                             "id": "evt-1",
                             "subject": "Client Workshop",
                             "bodyPreview": "Discuss roadmap",
                             "start": { "dateTime": "2026-05-02T08:00:00Z", "timeZone": "UTC" },
                             "end": { "dateTime": "2026-05-02T09:00:00Z", "timeZone": "UTC" },
                             "attendees": [ { "emailAddress": { "address": "alice@example.com" } } ],
                             "location": { "displayName": "Room A" }
                           }
                         ]
                       }
                       """;

            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);
        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(7);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("evt-1", events[0].Id);
        Assert.AreEqual("Client Workshop", events[0].Title);
        Assert.AreEqual("Discuss roadmap", events[0].Description);
        Assert.AreEqual("Room A", events[0].Location);
        CollectionAssert.AreEqual(new[] { "alice@example.com" }, events[0].AttendeeEmails.ToArray());
    }

    [TestMethod]
    public async Task GetEventsAsync_RefreshesExpiredToken_BeforeGraphCall()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("expired-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await dbContext.SaveChangesAsync();

        var tokenClient = new StubGraphOAuthTokenClient
        {
            RefreshedToken = new GraphOAuthTokenResponse("new-access-token", "new-refresh-token",
                DateTimeOffset.UtcNow.AddHours(1))
        };

        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            Assert.AreEqual("new-access-token", request.Headers.Authorization?.Parameter);

            const string json = "{ \"value\": [] }";
            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            tokenClient,
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);
        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(2);

        await source.GetEventsAsync(from, to, ownerId);

        var owner = await dbContext.CalendarOwners.SingleAsync(x => x.Id == ownerId);
        var unprotectedAccess = protector.Unprotect(owner.GraphAccessTokenProtected!);
        var unprotectedRefresh = protector.Unprotect(owner.GraphRefreshTokenProtected!);

        Assert.IsTrue(string.Equals("new-access-token", unprotectedAccess, StringComparison.Ordinal));
        Assert.IsTrue(string.Equals("new-refresh-token", unprotectedRefresh, StringComparison.Ordinal));
        Assert.AreEqual(1, tokenClient.RefreshCallCount);
    }

    [TestMethod]
    public async Task GetEventsAsync_ReturnsEmptyAndLogsWarning_WhenRefreshFails()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("expired-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await dbContext.SaveChangesAsync();

        var tokenClient = new StubGraphOAuthTokenClient
        {
            RefreshException = new InvalidOperationException("refresh-failed")
        };
        var logger = new CapturingLogger<GraphCalendarSource>();

        var source = CreateSource(
            dbContext,
            new HttpClient(new DelegatingHttpMessageHandler(_ =>
                throw new AssertFailedException("Graph endpoint should not be called.")))
            {
                BaseAddress = new Uri("https://graph.microsoft.com/")
            },
            tokenClient,
            logger,
            dataProtectionProvider);

        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.IsEmpty(events);
        Assert.Contains(entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("Graph access token refresh failed", StringComparison.Ordinal), logger.Entries);
    }

    [TestMethod]
    public async Task GetEventsAsync_RetriesGraphRequest_WhenInitialResponseIsUnauthorized()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("expired-access-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        });
        await dbContext.SaveChangesAsync();

        var tokenClient = new StubGraphOAuthTokenClient
        {
            RefreshedToken = new GraphOAuthTokenResponse("fresh-access-token", "refresh-token",
                DateTimeOffset.UtcNow.AddHours(1))
        };

        var callCount = 0;
        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            callCount++;

            if (callCount == 1)
            {
                Assert.AreEqual("expired-access-token", request.Headers.Authorization?.Parameter);
                return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }

            Assert.AreEqual("fresh-access-token", request.Headers.Authorization?.Parameter);
            const string json = "{ \"value\": [] }";
            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            tokenClient,
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.IsEmpty(events);
        Assert.AreEqual(2, callCount);
        Assert.AreEqual(1, tokenClient.RefreshCallCount);
    }

    [TestMethod]
    public async Task GetEventsAsync_SkipsManagedPlaceholderEvents()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        const string managedPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.Managed";
        using var handler = new DelegatingHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""
                {
                  "value": [
                    {
                      "id": "managed-1",
                      "subject": "Busy",
                      "start": { "dateTime": "2026-05-02T08:00:00Z", "timeZone": "UTC" },
                      "end": { "dateTime": "2026-05-02T09:00:00Z", "timeZone": "UTC" },
                      "singleValueExtendedProperties": [
                        { "id": "{{managedPropId}}", "value": "1" }
                      ]
                    },
                    {
                      "id": "evt-1",
                      "subject": "Client Workshop",
                      "start": { "dateTime": "2026-05-02T10:00:00Z", "timeZone": "UTC" },
                      "end": { "dateTime": "2026-05-02T11:00:00Z", "timeZone": "UTC" }
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        }));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };

        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var events = await source.GetEventsAsync(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero),
            ownerId);

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("evt-1", events[0].Id);
    }

    private static GraphCalendarSource CreateSource(
        AppDbContext dbContext,
        HttpClient httpClient,
        IGraphOAuthTokenClient tokenClient,
        ILogger<GraphCalendarSource> logger,
        IDataProtectionProvider dataProtectionProvider)
    {
        var instances = new FakeCalendarSourceInstanceService(ownerId =>
            dbContext.CalendarOwners.Any(owner => owner.Id == ownerId));
        return CreateSource(dbContext, httpClient, tokenClient, logger, dataProtectionProvider, instances);
    }

    private static GraphCalendarSource CreateSource(
        AppDbContext dbContext,
        HttpClient httpClient,
        IGraphOAuthTokenClient tokenClient,
        ILogger<GraphCalendarSource> logger,
        IDataProtectionProvider dataProtectionProvider,
        ICalendarSourceInstanceStore instances)
        => new(
            httpClient,
            dbContext,
            dataProtectionProvider,
            tokenClient,
            instances,
            logger);

    [TestMethod]
    public async Task GetEventsAsync_FollowsNextLink_UntilExhausted()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        const string page1Event = """
                                  {"id":"evt-p1","subject":"Page 1 event","start":{"dateTime":"2026-06-01T08:00:00Z","timeZone":"UTC"},"end":{"dateTime":"2026-06-01T09:00:00Z","timeZone":"UTC"}}
                                  """;
        const string page2Event = """
                                  {"id":"evt-p2","subject":"Page 2 event","start":{"dateTime":"2026-06-02T08:00:00Z","timeZone":"UTC"},"end":{"dateTime":"2026-06-02T09:00:00Z","timeZone":"UTC"}}
                                  """;

        var requestLog = new List<string>();
        var handler = new DelegatingHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            requestLog.Add(url);

            if (!url.Contains("$skiptoken=page2", StringComparison.Ordinal))
            {
                // First page: one event and a nextLink pointing to page 2
                var json =
                    $$"""{"value":[{{page1Event}}],"@odata.nextLink":"https://graph.microsoft.com/v1.0/me/calendarView?$skiptoken=page2"}""";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }

            // Page 2: one more event, no nextLink
            var page2Json = $$"""{"value":[{{page2Event}}]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(page2Json, Encoding.UTF8, "application/json")
            });
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var events = await source.GetEventsAsync(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ownerId);

        Assert.HasCount(2, events);
        Assert.AreEqual("Page 1 event", events[0].Title);
        Assert.AreEqual("Page 2 event", events[1].Title);
        Assert.HasCount(2, requestLog, "Expected initial request plus one next-page request.");
    }

    [TestMethod]
    public async Task GetEventsAsync_StopsWhenNextLinkRepeats()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        const string repeatedLink = "https://graph.microsoft.com/v1.0/me/calendarView?$skiptoken=repeat";
        var logger = new CapturingLogger<GraphCalendarSource>();
        var requestCount = 0;
        var handler = new DelegatingHttpMessageHandler(request =>
        {
            requestCount++;

            var json = $$"""
                         {
                           "value": [
                             {
                               "id": "evt-{{requestCount}}",
                               "subject": "Loop event {{requestCount}}",
                               "start": { "dateTime": "2026-06-01T08:00:00Z", "timeZone": "UTC" },
                               "end": { "dateTime": "2026-06-01T09:00:00Z", "timeZone": "UTC" }
                             }
                           ],
                           "@odata.nextLink": "{{repeatedLink}}"
                         }
                         """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            logger,
            dataProtectionProvider);

        var events = await source.GetEventsAsync(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ownerId);

        Assert.AreEqual(2, requestCount, "Expected the initial page plus one repeated nextLink page before stopping.");
        Assert.HasCount(2, events, "Expected pagination to stop early instead of looping forever.");
        Assert.Contains(
            entry => entry.LogLevel == LogLevel.Warning
                && entry.Message.Contains("repeated nextLink", StringComparison.Ordinal), logger.Entries,
            "Expected a warning when Graph pagination repeats the same nextLink.");
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_CreatesPlaceholderEvents_ForEachActiveSlot()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphRefreshTokenProtected = protector.Protect("refresh-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        var requestLog = new List<(HttpMethod Method, string Uri, string? Body)>();

        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync()
                : null;
            requestLog.Add((request.Method, request.RequestUri!.ToString(), body));

            // Simulate empty list of managed events on GET
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
                };
            }

            // Return Created for POST
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":\"new-event-id\"}", Encoding.UTF8, "application/json")
            };
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var from = new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 13, 10, 0, 0, TimeSpan.Zero);

        var slots = new List<BusySlot>
        {
            new("slot-1", from, to),
            new("slot-2", from.AddHours(2), to.AddHours(2))
        };

        var windowStart = from.AddHours(-1);
        var windowEnd = to.AddHours(3);
        await source.WriteBackSlotsAsync(ownerId, slots, "Busy", windowStart, windowEnd);

        var posts = requestLog.Where(r => r.Method == HttpMethod.Post).ToList();
        Assert.HasCount(2, posts, "Expected one POST per active shadow slot.");

        foreach (var (_, _, postBody) in posts)
        {
            Assert.IsNotNull(postBody);
            using var doc = JsonDocument.Parse(postBody);
            Assert.AreEqual("Busy", doc.RootElement.GetProperty("subject").GetString());
            Assert.AreEqual("busy", doc.RootElement.GetProperty("showAs").GetString());
            Assert.IsFalse(doc.RootElement.GetProperty("isReminderOn").GetBoolean());

            // Must carry both extended properties
            var extProps = doc.RootElement.GetProperty("singleValueExtendedProperties");
            Assert.AreEqual(2, extProps.GetArrayLength());
        }
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_DeletesStaleEvents_WhenNoLongerActiveSlot()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        const string staleGraphId = "stale-graph-event-id";
        const string slotIdPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.SlotId";

        var managedEventsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = staleGraphId,
                    subject = "Busy",
                    start = new { dateTime = "2026-05-10T08:00:00Z", timeZone = "UTC" },
                    end = new { dateTime = "2026-05-10T09:00:00Z", timeZone = "UTC" },
                    singleValueExtendedProperties = new[]
                    {
                        new { id = slotIdPropId, value = "stale-slot-id" }
                    }
                }
            }
        });

        var requestLog = new List<(HttpMethod Method, string Uri)>();

        var handler = new DelegatingHttpMessageHandler(request =>
        {
            requestLog.Add((request.Method, request.RequestUri!.ToString()));
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(managedEventsJson, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        // Window contains the stale event's start time (2026-05-10T08:00Z) so cleanup should fire.
        var windowStart = new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2026, 5, 10, 18, 0, 0, TimeSpan.Zero);
        await source.WriteBackSlotsAsync(ownerId, [], "Busy", windowStart, windowEnd);

        var deletes = requestLog.Where(r => r.Method == HttpMethod.Delete).ToList();
        Assert.HasCount(1, deletes);
        Assert.IsTrue(deletes[0].Uri.Contains(staleGraphId, StringComparison.Ordinal),
            "Expected DELETE request for the stale event id.");
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_SkipsWrite_WhenNoAccessToken()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner"
            // No access token
        });
        await dbContext.SaveChangesAsync();

        var called = false;
        var handler = new DelegatingHttpMessageHandler(_ =>
        {
            called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            new EphemeralDataProtectionProvider());

        var t = DateTimeOffset.UtcNow;
        await source.WriteBackSlotsAsync(ownerId, [new BusySlot("s1", t, t.AddHours(1))], "Busy", t, t.AddHours(1));

        Assert.IsFalse(called, "No Graph HTTP calls should be made when there is no token.");
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_UsesCustomPlaceholderTitle()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        string? capturedSubject = null;
        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
                };
            }

            var body = await request.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            capturedSubject = doc.RootElement.GetProperty("subject").GetString();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":\"new\"}", Encoding.UTF8, "application/json")
            };
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var from = DateTimeOffset.UtcNow;
        await source.WriteBackSlotsAsync(
            ownerId,
            [new BusySlot("s1", from, from.AddHours(1))],
            "Niet beschikbaar",
            from,
            from.AddHours(1));

        Assert.AreEqual("Niet beschikbaar", capturedSubject);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_DoesNotDeleteManagedEvent_WhenStartIsOutsideWindow()
    {
        // A placeholder event whose start is beyond the write-back window must be left alone to
        // avoid churn: the event will be re-evaluated once the advancing window reaches it.
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        const string futureGraphId = "future-graph-event-id";
        const string slotIdPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.SlotId";
        // The managed event starts 30 days from now — well outside the 14-day window.
        var futureStart = DateTimeOffset.UtcNow.AddDays(30);
        var managedEventsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = futureGraphId,
                    subject = "Busy",
                    start = new { dateTime = futureStart.ToString("O"), timeZone = "UTC" },
                    end = new { dateTime = futureStart.AddHours(1).ToString("O"), timeZone = "UTC" },
                    singleValueExtendedProperties = new[]
                    {
                        new { id = slotIdPropId, value = "future-slot-id" }
                    }
                }
            }
        });

        var deleteCalledCount = 0;
        var handler = new DelegatingHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Delete) deleteCalledCount++;
            return Task.FromResult(request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(managedEventsJson, Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        // Window covers today only — the future event is outside.
        var windowStart = DateTimeOffset.UtcNow;
        var windowEnd = DateTimeOffset.UtcNow.AddDays(14);
        await source.WriteBackSlotsAsync(ownerId, [], "Busy", windowStart, windowEnd);

        Assert.AreEqual(0, deleteCalledCount,
            "Placeholder events beyond the write-back window must not be deleted.");
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_QueriesManagedEventsOnlyWithinWindow()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Owner",
            GraphAccessTokenProtected = protector.Protect("access-token"),
            GraphTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        string? requestUri = null;
        var handler = new DelegatingHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
            });
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var windowStart = new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(2026, 8, 8, 0, 0, 0, TimeSpan.Zero);

        await source.WriteBackSlotsAsync(ownerId, [], "Busy", windowStart, windowEnd);

        Assert.IsNotNull(requestUri);
        Assert.IsTrue(
            requestUri.Contains(
                Uri.EscapeDataString(windowStart.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
                StringComparison.Ordinal),
            "Expected managed-event query to include the write-back window start.");
        Assert.IsTrue(
            requestUri.Contains(Uri.EscapeDataString(windowEnd.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
                StringComparison.Ordinal),
            "Expected managed-event query to include the write-back window end.");
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_ForSourceInstance_CreatesPlaceholderEvents()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        await dbContext.CalendarOwners.AddAsync(new CalendarOwner { Id = ownerId, Name = "Owner" });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");
        var instances = new FakeCalendarSourceInstanceService(id => id == ownerId);
        var created = await instances.CreateAsync(
            ownerId,
            new CreateCalendarSourceInstanceInput(
                "graph",
                "Graph",
                "{\"calendarId\":\"primary\"}",
                JsonSerializer.Serialize(new GraphCalendarSource.GraphSourceSecretData(
                    protector.Protect("access-token"),
                    protector.Protect("refresh-token"),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddHours(1),
                    DateTimeOffset.UtcNow))));
        Assert.IsNotNull(created);

        var instance = await instances.GetAsync(ownerId, created.Id);
        Assert.IsNotNull(instance);

        var requestLog = new List<(HttpMethod Method, string Uri, string? Body)>();
        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            requestLog.Add((request.Method, request.RequestUri!.ToString(), body));

            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":\"new-event-id\"}", Encoding.UTF8, "application/json")
            };
        });

        var source = CreateSource(
            dbContext,
            new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") },
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider,
            instances);

        var from = new DateTimeOffset(2026, 5, 13, 9, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 13, 10, 0, 0, TimeSpan.Zero);

        await source.WriteBackSlotsAsync(instance, [new BusySlot("slot-1", from, to)], "Busy", from.AddHours(-1), to.AddHours(1));

        var post = requestLog.Single(entry => entry.Method == HttpMethod.Post);
        using var doc = JsonDocument.Parse(post.Body!);
        Assert.AreEqual("Busy", doc.RootElement.GetProperty("subject").GetString());
        Assert.AreEqual(2, doc.RootElement.GetProperty("singleValueExtendedProperties").GetArrayLength());
    }

    private sealed class StubGraphOAuthTokenClient : IGraphOAuthTokenClient
    {
        public GraphOAuthTokenResponse RefreshedToken { get; set; } =
            new("access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1));

        public Exception? RefreshException { get; set; }
        public int RefreshCallCount { get; private set; }

        public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string authorizationCode,
            string redirectUri, CancellationToken ct = default)
            => Task.FromResult(RefreshedToken);

        public Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken,
            CancellationToken ct = default)
        {
            RefreshCallCount++;
            return RefreshException is not null ? throw RefreshException : Task.FromResult(RefreshedToken);
        }
    }


    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);

    private sealed class DelegatingHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handler(request);
    }
}
