using System.Globalization;
using System.Net;
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

            return await Task.FromResult(TestHttpResponses.Json(HttpStatusCode.OK, json));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);
        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(7);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.HasCount(1, events);
        Assert.AreEqual("evt-1", events[0].Id);
        Assert.AreEqual("Client Workshop", events[0].Title);
        Assert.AreEqual("Discuss roadmap", events[0].Description);
        Assert.AreEqual("Room A", events[0].Location);
        CollectionAssert.AreEqual(new[] { "alice@example.com" }, events[0].AttendeeEmails.ToArray());
    }

    [TestMethod]
    public async Task GetReadinessAsync_ForReadOnlySourceInstance_ShowsReadOnlyStatus()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        using var httpClient = new HttpClient(new DelegatingHttpMessageHandler(_ =>
            Task.FromResult(TestHttpResponses.Create(HttpStatusCode.OK))));
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");

        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var instance = new CalendarSourceInstanceContext(
            instanceId,
            ownerId,
            "graph",
            "Outlook",
            true,
            null,
            JsonSerializer.Serialize(new GraphCalendarSource.GraphSourceSecretData(
                "protected-access-token",
                "protected-refresh-token",
                "https://graph.microsoft.com/Calendars.Read offline_access",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1),
                DateTimeOffset.UtcNow)),
            false);

        var readiness = await source.GetReadinessAsync(instance);

        Assert.IsTrue(readiness.IsReady);
        Assert.AreEqual("Connected (read-only).", readiness.Title);
        Assert.AreEqual(
            "Outlook consent is read-only; write-back placeholders are disabled for this source instance.",
            readiness.Detail);
    }

    [TestMethod]
    public async Task GetReadinessAsync_ForSourceInstance_ReadOnlyChoiceOverridesBroaderReturnedScopes()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        using var httpClient = new HttpClient(new DelegatingHttpMessageHandler(_ =>
            Task.FromResult(TestHttpResponses.Create(HttpStatusCode.OK))));
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");

        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var instance = new CalendarSourceInstanceContext(
            instanceId,
            ownerId,
            "graph",
            "Outlook",
            true,
            null,
            JsonSerializer.Serialize(new GraphCalendarSource.GraphSourceSecretData(
                "protected-access-token",
                "protected-refresh-token",
                "https://graph.microsoft.com/Calendars.ReadWrite offline_access",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1),
                DateTimeOffset.UtcNow,
                GraphConsentAccessLevel.ReadOnly)),
            false);

        var readiness = await source.GetReadinessAsync(instance);

        Assert.IsTrue(readiness.IsReady);
        Assert.AreEqual("Connected (read-only).", readiness.Title);
    }

    [TestMethod]
    public async Task GetEventsAsync_MapsGraphAllDayEvent_AsExclusiveUtcDateRange()
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

        var handler = new DelegatingHttpMessageHandler(_ => Task.FromResult(TestHttpResponses.Json(
            HttpStatusCode.OK,
            """
            {
              "value": [
                {
                  "id": "evt-allday-1",
                  "subject": "Holiday",
                  "isAllDay": true,
                  "start": { "dateTime": "2026-06-04T00:00:00.0000000", "timeZone": "W. Europe Standard Time" },
                  "end": { "dateTime": "2026-06-05T00:00:00.0000000", "timeZone": "W. Europe Standard Time" }
                }
              ]
            }
            """)));

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var events = await source.GetEventsAsync(
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero),
            ownerId);

        Assert.HasCount(1, events);
        Assert.IsTrue(events[0].IsAllDay);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero), events[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero), events[0].End);
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
            RefreshedToken = new GraphOAuthTokenResponse("new-access-token", "new-refresh-token", "https://graph.microsoft.com/Calendars.ReadWrite offline_access",
                DateTimeOffset.UtcNow.AddHours(1))
        };

        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            Assert.AreEqual("new-access-token", request.Headers.Authorization?.Parameter);

            const string json = "{ \"value\": [] }";
            return await Task.FromResult(TestHttpResponses.Json(HttpStatusCode.OK, json));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
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

        var handler = new DelegatingHttpMessageHandler(_ =>
            throw new AssertFailedException("Graph endpoint should not be called."));
        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
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
            RefreshedToken = new GraphOAuthTokenResponse("fresh-access-token", "refresh-token", "https://graph.microsoft.com/Calendars.ReadWrite offline_access", DateTimeOffset.UtcNow.AddHours(1))
        };

        var callCount = 0;
        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            callCount++;

            if (callCount == 1)
            {
                Assert.AreEqual("expired-access-token", request.Headers.Authorization?.Parameter);
                return await Task.FromResult(TestHttpResponses.Create(HttpStatusCode.Unauthorized));
            }

            Assert.AreEqual("fresh-access-token", request.Headers.Authorization?.Parameter);
            const string json = "{ \"value\": [] }";
            return await Task.FromResult(TestHttpResponses.Json(HttpStatusCode.OK, json));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
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
    public async Task GetEventsAsync_ReusesRefreshedToken_ForNextLinkRequests()
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
            RefreshedToken = new GraphOAuthTokenResponse("fresh-access-token", "refresh-token", "https://graph.microsoft.com/Calendars.ReadWrite offline_access", DateTimeOffset.UtcNow.AddHours(1))
        };

        var seenTokens = new List<string?>();
        var createdResponses = new List<HttpResponseMessage>();
        var handler = new DelegatingHttpMessageHandler(request =>
        {
            var token = request.Headers.Authorization?.Parameter;
            seenTokens.Add(token);

            var requestUri = request.RequestUri!.ToString();
            if (seenTokens.Count == 1)
            {
                var unauthorized = TestHttpResponses.Create(HttpStatusCode.Unauthorized);
                createdResponses.Add(unauthorized);
                return Task.FromResult(unauthorized);
            }

            if (!requestUri.Contains("$skiptoken=page2", StringComparison.Ordinal))
            {
                const string page1Json = """
                                         {
                                           "value": [
                                             {
                                               "id": "evt-p1",
                                               "subject": "Page 1 event",
                                               "start": { "dateTime": "2026-06-01T08:00:00Z", "timeZone": "UTC" },
                                               "end": { "dateTime": "2026-06-01T09:00:00Z", "timeZone": "UTC" }
                                             }
                                           ],
                                           "@odata.nextLink": "https://graph.microsoft.com/v1.0/me/calendarView?$skiptoken=page2"
                                         }
                                         """;
                var page1Response = TestHttpResponses.Json(HttpStatusCode.OK, page1Json);
                createdResponses.Add(page1Response);
                return Task.FromResult(page1Response);
            }

            const string page2Json = """
                                     {
                                       "value": [
                                         {
                                           "id": "evt-p2",
                                           "subject": "Page 2 event",
                                           "start": { "dateTime": "2026-06-02T08:00:00Z", "timeZone": "UTC" },
                                           "end": { "dateTime": "2026-06-02T09:00:00Z", "timeZone": "UTC" }
                                         }
                                       ]
                                     }
                                     """;
            var page2Response = TestHttpResponses.Json(HttpStatusCode.OK, page2Json);
            createdResponses.Add(page2Response);
            return Task.FromResult(page2Response);
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            tokenClient,
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var from = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(7);

        var events = await source.GetEventsAsync(from, to, ownerId);

        CollectionAssert.AreEqual(
            new[] { "expired-access-token", "fresh-access-token", "fresh-access-token" },
            seenTokens.ToArray());
        Assert.AreEqual(1, tokenClient.RefreshCallCount);
        Assert.HasCount(2, events);
        Assert.AreEqual("Page 1 event", events[0].Title);
        Assert.AreEqual("Page 2 event", events[1].Title);
        foreach (var response in createdResponses)
        {
            response.Dispose();
        }
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
        using var handler = new DelegatingHttpMessageHandler(_ => Task.FromResult(TestHttpResponses.Json(
            HttpStatusCode.OK,
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
            """)));
        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");

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
        static HttpResponseMessage CreateJsonOkResponse(string payload)
            => TestHttpResponses.Json(HttpStatusCode.OK, payload);

        var handler = new DelegatingHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            requestLog.Add(url);

            if (!url.Contains("$skiptoken=page2", StringComparison.Ordinal))
            {
                // First page: one event and a nextLink pointing to page 2
                var json =
                    $$"""{"value":[{{page1Event}}],"@odata.nextLink":"https://graph.microsoft.com/v1.0/me/calendarView?$skiptoken=page2"}""";
                return Task.FromResult(CreateJsonOkResponse(json));
            }

            // Page 2: one more event, no nextLink
            var page2Json = $$"""{"value":[{{page2Event}}]}""";
            return Task.FromResult(CreateJsonOkResponse(page2Json));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
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
        var handler = new DelegatingHttpMessageHandler(_ =>
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

            return Task.FromResult(TestHttpResponses.Json(HttpStatusCode.OK, json));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
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
                return TestHttpResponses.Json(HttpStatusCode.OK, "{\"value\":[]}");
            }

            // Return Created for POST
            return TestHttpResponses.Json(HttpStatusCode.Created, "{\"id\":\"new-event-id\"}");
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
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
        const string managedPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.Managed";
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
                        new { id = managedPropId, value = "1" },
                        new { id = slotIdPropId, value = "stale-slot-id" }
                    }
                }
            }
        });

        var requestLog = new List<(HttpMethod Method, string Uri)>();

        var handler = new DelegatingHttpMessageHandler(request =>
        {
            requestLog.Add((request.Method, request.RequestUri!.ToString()));
            if (request.Method != HttpMethod.Get)
                return Task.FromResult(TestHttpResponses.Create(HttpStatusCode.NoContent));
            var response = TestHttpResponses.Json(HttpStatusCode.OK, managedEventsJson);
            return Task.FromResult(response);

        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
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
            return Task.FromResult(TestHttpResponses.Create(HttpStatusCode.OK));
        });
        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");

        var source = CreateSource(
            dbContext,
            httpClient,
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
        const string placeholderTitle = "Custom placeholder title";

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
                return TestHttpResponses.Json(HttpStatusCode.OK, "{\"value\":[]}");
            }

            var body = await request.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            capturedSubject = doc.RootElement.GetProperty("subject").GetString();
            return TestHttpResponses.Json(HttpStatusCode.Created, "{\"id\":\"new\"}");
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var from = DateTimeOffset.UtcNow;
        await source.WriteBackSlotsAsync(
            ownerId,
            [new BusySlot("s1", from, from.AddHours(1))],
            placeholderTitle,
            from,
            from.AddHours(1));

        Assert.AreEqual(placeholderTitle, capturedSubject);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_AppendsSourceNameToOutboundSubject()
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
                return TestHttpResponses.Json(HttpStatusCode.OK, "{\"value\":[]}");

            var body = await request.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            capturedSubject = doc.RootElement.GetProperty("subject").GetString();
            return TestHttpResponses.Json(HttpStatusCode.Created, "{\"id\":\"new\"}");
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var from = DateTimeOffset.UtcNow;
        await source.WriteBackSlotsAsync(
            ownerId,
            [new BusySlot("s1", from, from.AddHours(1), SourceName: "CA")],
            "Busy",
            from,
            from.AddHours(1));

        Assert.AreEqual("Busy (CA)", capturedSubject);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_AppendsSourceNameToOutboundSubject_ForAllDayEvents()
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
        bool? capturedIsAllDay = null;
        string? capturedStart = null;
        string? capturedEnd = null;
        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            if (request.Method == HttpMethod.Get)
                return TestHttpResponses.Json(HttpStatusCode.OK, "{\"value\":[]}");

            var body = await request.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            capturedSubject = doc.RootElement.GetProperty("subject").GetString();
            capturedIsAllDay = doc.RootElement.GetProperty("isAllDay").GetBoolean();
            capturedStart = doc.RootElement.GetProperty("start").GetProperty("dateTime").GetString();
            capturedEnd = doc.RootElement.GetProperty("end").GetProperty("dateTime").GetString();
            return TestHttpResponses.Json(HttpStatusCode.Created, "{\"id\":\"new\"}");
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var start = new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);
        await source.WriteBackSlotsAsync(
            ownerId,
            [new BusySlot("s1", start, end, SourceName: "CA", IsAllDay: true)],
            "Busy",
            start,
            end);

        Assert.AreEqual("Busy (CA)", capturedSubject);
        Assert.IsTrue(capturedIsAllDay);
        Assert.AreEqual("2026-06-04T00:00:00.0000000", capturedStart);
        Assert.AreEqual("2026-06-05T00:00:00.0000000", capturedEnd);
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_PatchesExistingAllDayPlaceholder_WithSourceNameInSubject()
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

        const string managedPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.Managed";
        const string slotIdPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.SlotId";

        var requestLog = new List<(HttpMethod Method, string Uri)>();
        string? patchedSubject = null;
        bool? patchedIsAllDay = null;
        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            requestLog.Add((request.Method, request.RequestUri!.ToString()));

            if (request.Method == HttpMethod.Get)
            {
                return TestHttpResponses.Json(
                    HttpStatusCode.OK,
                    $$"""
                    {
                      "value": [
                        {
                          "id": "managed-1",
                          "subject": "Busy",
                          "isAllDay": true,
                          "start": { "dateTime": "2026-06-04T00:00:00.0000000", "timeZone": "UTC" },
                          "end": { "dateTime": "2026-06-05T00:00:00.0000000", "timeZone": "UTC" },
                          "singleValueExtendedProperties": [
                            { "id": "{{managedPropId}}", "value": "1" },
                            { "id": "{{slotIdPropId}}", "value": "slot-1" }
                          ]
                        }
                      ]
                    }
                    """);
            }

            if (request.Method == HttpMethod.Patch)
            {
                var body = await request.Content!.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                patchedSubject = doc.RootElement.GetProperty("subject").GetString();
                patchedIsAllDay = doc.RootElement.GetProperty("isAllDay").GetBoolean();
                return TestHttpResponses.Create(HttpStatusCode.OK);
            }

            throw new AssertFailedException($"Unexpected request method {request.Method}.");
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var start = new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);
        await source.WriteBackSlotsAsync(
            ownerId,
            [new BusySlot("slot-1", start, end, SourceName: "CA", IsAllDay: true)],
            "Busy",
            start,
            end);

        Assert.AreEqual("Busy (CA)", patchedSubject);
        Assert.IsTrue(patchedIsAllDay);
        Assert.ContainsSingle(entry => entry.Method == HttpMethod.Get, requestLog);
        Assert.ContainsSingle(entry => entry.Method == HttpMethod.Patch, requestLog);
        Assert.AreEqual(0, requestLog.Count(entry => entry.Method == HttpMethod.Post));
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_PatchesLegacyAllDayPlaceholderWithoutSlotId_WithSourceNameInSubject()
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

        const string managedPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.Managed";
        const string slotIdPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.SlotId";

        var requestLog = new List<(HttpMethod Method, string Uri)>();
        string? patchedSubject = null;
        string? patchedSlotId = null;
        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            requestLog.Add((request.Method, request.RequestUri!.ToString()));

            if (request.Method == HttpMethod.Get)
            {
                return TestHttpResponses.Json(
                    HttpStatusCode.OK,
                    $$"""
                    {
                      "value": [
                        {
                          "id": "managed-legacy-1",
                          "subject": "Busy",
                          "isAllDay": true,
                          "start": { "dateTime": "2026-06-04T00:00:00.0000000", "timeZone": "UTC" },
                          "end": { "dateTime": "2026-06-05T00:00:00.0000000", "timeZone": "UTC" },
                          "singleValueExtendedProperties": [
                            { "id": "{{managedPropId}}", "value": "1" }
                          ]
                        }
                      ]
                    }
                    """);
            }

            if (request.Method == HttpMethod.Patch)
            {
                var body = await request.Content!.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                patchedSubject = doc.RootElement.GetProperty("subject").GetString();
                patchedSlotId = doc.RootElement
                    .GetProperty("singleValueExtendedProperties")
                    .EnumerateArray()
                    .FirstOrDefault(property => string.Equals(property.GetProperty("id").GetString(), slotIdPropId, StringComparison.Ordinal))
                    .GetProperty("value")
                    .GetString();
                return TestHttpResponses.Create(HttpStatusCode.OK);
            }

            throw new AssertFailedException($"Unexpected request method {request.Method}.");
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var start = new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);
        await source.WriteBackSlotsAsync(
            ownerId,
            [new BusySlot("slot-1", start, end, SourceName: "CA", IsAllDay: true)],
            "Busy",
            start,
            end);

        Assert.AreEqual("Busy (CA)", patchedSubject);
        Assert.AreEqual("slot-1", patchedSlotId);
        Assert.ContainsSingle(entry => entry.Method == HttpMethod.Get, requestLog);
        Assert.ContainsSingle(entry => entry.Method == HttpMethod.Patch, requestLog);
        Assert.AreEqual(0, requestLog.Count(entry => entry.Method == HttpMethod.Post));
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_UsesCalendarViewQueryWithManagedAndSlotProperties()
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
            if (request.Method == HttpMethod.Get)
                requestUri = request.RequestUri!.ToString();
            return Task.FromResult(TestHttpResponses.Json(HttpStatusCode.OK, "{\"value\":[]}"));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        var start = new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);
        await source.WriteBackSlotsAsync(
            ownerId,
            [new BusySlot("slot-1", start, end, SourceName: "CA", IsAllDay: true)],
            "Busy",
            start,
            end);

        Assert.IsNotNull(requestUri);
        Assert.IsTrue(requestUri.Contains("/v1.0/me/calendarView", StringComparison.Ordinal));
        Assert.IsTrue(requestUri.Contains(Uri.EscapeDataString("ObfusCal.Managed"), StringComparison.Ordinal));
        Assert.IsTrue(requestUri.Contains(Uri.EscapeDataString("ObfusCal.SlotId"), StringComparison.Ordinal));
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
        const string managedPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.Managed";
        const string slotIdPropId = "String {e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0} Name ObfusCal.SlotId";
        // The managed event starts 30 days from now - well outside the 14-day window.
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
                        new { id = managedPropId, value = "1" },
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
                ? TestHttpResponses.Json(HttpStatusCode.OK, managedEventsJson)
                : TestHttpResponses.Create(HttpStatusCode.NoContent));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider);

        // Window covers today only - the future event is outside.
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
            return Task.FromResult(TestHttpResponses.Json(HttpStatusCode.OK, "{\"value\":[]}"));
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
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
    public async Task WriteBackSlotsAsync_ForSourceInstance_SkipsWrite_WhenScopesAreNull()
    {
        // An instance whose GrantedScopes field is null (e.g. data created before scope tracking was
        // added, or missing JSON field) must be treated as read-only.  No Graph mutations should occur.
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
                    GrantedScopes: null,        // <-- no scopes stored
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddHours(1),
                    DateTimeOffset.UtcNow))));
        Assert.IsNotNull(created);

        var instance = await instances.GetAsync(ownerId, created.Id);
        Assert.IsNotNull(instance);

        var called = false;
        var handler = new DelegatingHttpMessageHandler(_ =>
        {
            called = true;
            return Task.FromResult(TestHttpResponses.Create(HttpStatusCode.OK));
        });
        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider,
            instances);

        var from = DateTimeOffset.UtcNow;
        var to = from.AddHours(1);
        await source.WriteBackSlotsAsync(instance, [new BusySlot("slot-1", from, to)], "Busy", from, to);

        Assert.IsFalse(called,
            "No Graph HTTP calls should be made for an instance with null GrantedScopes (read-only fallback).");
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_ForSourceInstance_SkipsWrite_WhenReadOnlyConsent()
    {
        // An instance consented via the read-only flow stores Calendars.Read (no ReadWrite).
        // Write-back must be suppressed so no DELETE or POST calls reach the Graph API.
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
                    "https://graph.microsoft.com/Calendars.Read offline_access",   // read-only scope
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddHours(1),
                    DateTimeOffset.UtcNow))));
        Assert.IsNotNull(created);

        var instance = await instances.GetAsync(ownerId, created.Id);
        Assert.IsNotNull(instance);

        var called = false;
        var handler = new DelegatingHttpMessageHandler(_ =>
        {
            called = true;
            return Task.FromResult(TestHttpResponses.Create(HttpStatusCode.OK));
        });
        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider,
            instances);

        var from = DateTimeOffset.UtcNow;
        var to = from.AddHours(1);
        await source.WriteBackSlotsAsync(instance, [new BusySlot("slot-1", from, to)], "Busy", from, to);

        Assert.IsFalse(called,
            "No Graph HTTP calls should be made for a source instance consented with Calendars.Read only.");
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_ForSourceInstance_SkipsWrite_WhenReadOnlyChoiceOverridesBroaderReturnedScopes()
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
                    "https://graph.microsoft.com/Calendars.ReadWrite offline_access",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddHours(1),
                    DateTimeOffset.UtcNow,
                    GraphConsentAccessLevel.ReadOnly))));
        Assert.IsNotNull(created);

        var instance = await instances.GetAsync(ownerId, created.Id);
        Assert.IsNotNull(instance);

        var called = false;
        var handler = new DelegatingHttpMessageHandler(_ =>
        {
            called = true;
            return Task.FromResult(TestHttpResponses.Create(HttpStatusCode.OK));
        });
        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
            new StubGraphOAuthTokenClient(),
            new CapturingLogger<GraphCalendarSource>(),
            dataProtectionProvider,
            instances);

        var from = DateTimeOffset.UtcNow;
        var to = from.AddHours(1);
        await source.WriteBackSlotsAsync(instance, [new BusySlot("slot-1", from, to)], "Busy", from, to);

        Assert.IsFalse(called,
            "No Graph HTTP calls should be made when the latest source-instance consent choice is read-only.");
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
                    "https://graph.microsoft.com/Calendars.ReadWrite offline_access",
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
                return TestHttpResponses.Json(HttpStatusCode.OK, "{\"value\":[]}");
            }

            return TestHttpResponses.Json(HttpStatusCode.Created, "{\"id\":\"new-event-id\"}");
        });

        using var httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
        var source = CreateSource(
            dbContext,
            httpClient,
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
            new("access-token", "refresh-token", "https://graph.microsoft.com/Calendars.ReadWrite offline_access", DateTimeOffset.UtcNow.AddHours(1));

        public Exception? RefreshException { get; set; }
        public int RefreshCallCount { get; private set; }

        public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string authorizationCode,
            string redirectUri, string? scope = null, CancellationToken ct = default)
            => Task.FromResult(RefreshedToken);

        public Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken,
            string? scope = null,
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
