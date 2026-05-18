using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Security;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class GoogleCalendarSourceCoreTests
{
    [TestMethod]
    public async Task GetEventsAsync_MapsGoogleResponse()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        dbContext.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Owner" });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var secretProtector = new CalendarSourceSecretProtector(dataProtectionProvider);
        var instances = new FakeCalendarSourceInstanceService(id => id == ownerId);

        var created = await instances.CreateAsync(ownerId,
            new CreateCalendarSourceInstanceInput(
                "google",
                "Google Calendar",
                "{\"calendarId\":\"primary\"}",
                SerializeSecret(secretProtector, "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1))));
        Assert.IsNotNull(created);

        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            StringAssert.Contains(request.RequestUri!.AbsoluteUri, "/calendar/v3/calendars/primary/events");
            Assert.AreEqual("Bearer", request.Headers.Authorization?.Scheme);
            Assert.AreEqual("access-token", request.Headers.Authorization?.Parameter);

            var json = """
                       {
                         "items": [
                           {
                             "id": "google-evt-1",
                             "summary": "Project Sync",
                             "description": "Discuss blockers",
                             "start": { "dateTime": "2026-06-10T08:00:00Z" },
                             "end": { "dateTime": "2026-06-10T09:00:00Z" },
                             "attendees": [ { "email": "alice@example.com" } ],
                             "location": "Room B"
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
            instances,
            secretProtector,
            new StubGoogleOAuthTokenClient(),
            new HttpClient(handler),
            new CapturingLogger<GoogleCalendarSourceCore>());

        var from = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual("google-evt-1", events[0].Id);
        Assert.AreEqual("Project Sync", events[0].Title);
        Assert.AreEqual("Discuss blockers", events[0].Description);
        Assert.AreEqual("Room B", events[0].Location);
    }

    [TestMethod]
    public async Task GetEventsAsync_SkipsManagedPlaceholderEvents()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        dbContext.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Owner" });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var secretProtector = new CalendarSourceSecretProtector(dataProtectionProvider);
        var instances = new FakeCalendarSourceInstanceService(id => id == ownerId);

        var created = await instances.CreateAsync(ownerId,
            new CreateCalendarSourceInstanceInput(
                "google",
                "Google Calendar",
                "{\"calendarId\":\"primary\"}",
                SerializeSecret(secretProtector, "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1))));
        Assert.IsNotNull(created);

        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "items": [
                    {
                      "id": "managed-1",
                      "summary": "Busy",
                      "start": { "dateTime": "2026-06-10T08:00:00Z" },
                      "end": { "dateTime": "2026-06-10T09:00:00Z" },
                      "extendedProperties": {
                        "private": {
                          "ObfusCal.Managed": "1",
                          "ObfusCal.SlotId": "slot-1"
                        }
                      }
                    },
                    {
                      "id": "google-evt-2",
                      "summary": "Real event",
                      "start": { "dateTime": "2026-06-10T10:00:00Z" },
                      "end": { "dateTime": "2026-06-10T11:00:00Z" }
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json")
        };
        var handler = new DelegatingHttpMessageHandler(_ => Task.FromResult(response));
        using var httpClient = new HttpClient(handler);

        var source = CreateSource(
            dbContext,
            instances,
            secretProtector,
            new StubGoogleOAuthTokenClient(),
            httpClient,
            new CapturingLogger<GoogleCalendarSourceCore>());

        var events = await source.GetEventsAsync(
            new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero),
            ownerId);

        Assert.HasCount(1, events);
        Assert.AreEqual("google-evt-2", events[0].Id);
    }

    [TestMethod]
    public async Task GetEventsAsync_RefreshesExpiredToken_BeforeGoogleCall()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        dbContext.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Owner" });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var secretProtector = new CalendarSourceSecretProtector(dataProtectionProvider);
        var instances = new FakeCalendarSourceInstanceService(id => id == ownerId);

        var created = await instances.CreateAsync(ownerId,
            new CreateCalendarSourceInstanceInput(
                "google",
                "Google Calendar",
                "{\"calendarId\":\"primary\"}",
                SerializeSecret(secretProtector, "expired-access", "refresh-token", DateTimeOffset.UtcNow.AddMinutes(-10))));
        Assert.IsNotNull(created);

        var tokenClient = new StubGoogleOAuthTokenClient
        {
            RefreshedToken = new GoogleOAuthTokenResponse("fresh-access", "fresh-refresh", DateTimeOffset.UtcNow.AddHours(1))
        };

        var handler = new DelegatingHttpMessageHandler(async request =>
        {
            Assert.AreEqual("fresh-access", request.Headers.Authorization?.Parameter);
            const string json = "{ \"items\": [] }";
            return await Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var source = CreateSource(
            dbContext,
            instances,
            secretProtector,
            tokenClient,
            new HttpClient(handler),
            new CapturingLogger<GoogleCalendarSourceCore>());

        var from = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var events = await source.GetEventsAsync(from, to, ownerId);
        Assert.AreEqual(0, events.Count);

        var instance = await instances.GetFirstAsync(ownerId, "google");
        Assert.IsNotNull(instance);
        var secret = DeserializeSecret(instance.SecretDataJson);
        Assert.IsNotNull(secret);
        Assert.AreEqual("fresh-access", secretProtector.Unprotect(secret.ProtectedAccessToken!));
        Assert.AreEqual("fresh-refresh", secretProtector.Unprotect(secret.ProtectedRefreshToken!));
        Assert.AreEqual(1, tokenClient.RefreshCallCount);
    }

    [TestMethod]
    public async Task GetReadinessAsync_ReturnsNotReady_WhenNoCredentialsExist()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        dbContext.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Owner" });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var secretProtector = new CalendarSourceSecretProtector(dataProtectionProvider);
        var instances = new FakeCalendarSourceInstanceService(id => id == ownerId);

        var created = await instances.CreateAsync(ownerId,
            new CreateCalendarSourceInstanceInput("google", "Google Calendar", "{\"calendarId\":\"primary\"}"));
        Assert.IsNotNull(created);

        var source = CreateSource(
            dbContext,
            instances,
            secretProtector,
            new StubGoogleOAuthTokenClient(),
            new HttpClient(new DelegatingHttpMessageHandler(_ => throw new AssertFailedException("Should not call Google API."))),
            new CapturingLogger<GoogleCalendarSourceCore>());

        var readiness = await source.GetReadinessAsync(ownerId);

        Assert.IsFalse(readiness.IsReady);
        StringAssert.Contains(readiness.Title, "Google consent required");
    }

    [TestMethod]
    public async Task WriteBackSlotsAsync_CreatesPlaceholderEventsForInstance()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        dbContext.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Owner" });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var secretProtector = new CalendarSourceSecretProtector(dataProtectionProvider);
        var instances = new FakeCalendarSourceInstanceService(id => id == ownerId);

        var created = await instances.CreateAsync(ownerId,
            new CreateCalendarSourceInstanceInput(
                "google",
                "Google Calendar",
                "{\"calendarId\":\"primary\"}",
                SerializeSecret(secretProtector, "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1))));
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
                    Content = new StringContent("{\"items\":[]}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":\"new-google-event\"}", Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);

        var source = CreateSource(
            dbContext,
            instances,
            secretProtector,
            new StubGoogleOAuthTokenClient(),
            httpClient,
            new CapturingLogger<GoogleCalendarSourceCore>());

        var from = new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(1);

        await source.WriteBackSlotsAsync(
            instance,
            [new Domain.Models.BusySlot("slot-1", from, to)],
            "Busy",
            from.AddHours(-1),
            to.AddHours(1));

        var post = requestLog.Single(entry => entry.Method == HttpMethod.Post);
        StringAssert.Contains(post.Uri, "/calendar/v3/calendars/primary/events");
        Assert.IsNotNull(post.Body);

        using var doc = JsonDocument.Parse(post.Body);
        Assert.AreEqual("Busy", doc.RootElement.GetProperty("summary").GetString());
        Assert.AreEqual("1", doc.RootElement
            .GetProperty("extendedProperties")
            .GetProperty("private")
            .GetProperty("ObfusCal.Managed")
            .GetString());
        Assert.AreEqual("slot-1", doc.RootElement
            .GetProperty("extendedProperties")
            .GetProperty("private")
            .GetProperty("ObfusCal.SlotId")
            .GetString());
    }

    [TestMethod]
    public async Task GetEventsAsync_Throws_WhenGoogleApiBaseUrlIsMissing()
    {
        await using var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        dbContext.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Owner" });
        await dbContext.SaveChangesAsync();

        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var secretProtector = new CalendarSourceSecretProtector(dataProtectionProvider);
        var instances = new FakeCalendarSourceInstanceService(id => id == ownerId);

        var created = await instances.CreateAsync(ownerId,
            new CreateCalendarSourceInstanceInput(
                "google",
                "Google Calendar",
                "{\"calendarId\":\"primary\"}",
                SerializeSecret(secretProtector, "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1))));
        Assert.IsNotNull(created);

        var source = CreateSource(
            dbContext,
            instances,
            secretProtector,
            new StubGoogleOAuthTokenClient(),
            new HttpClient(new DelegatingHttpMessageHandler(_ => throw new AssertFailedException("Should not call Google API."))),
            new CapturingLogger<GoogleCalendarSourceCore>(),
            new GoogleConsentOptions());

        var from = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => source.GetEventsAsync(from, to, ownerId));
        Assert.Contains("GoogleConsent:ApiBaseUrl is required", exception.Message);
    }

    private static GoogleCalendarSourceCore CreateSource(
        AppDbContext dbContext,
        FakeCalendarSourceInstanceService instances,
        ICalendarSourceSecretProtector secretProtector,
        IGoogleOAuthTokenClient tokenClient,
        HttpClient httpClient,
        ILogger<GoogleCalendarSourceCore> logger,
        GoogleConsentOptions? googleConsentOptions = null)
    {
        var options = Options.Create(googleConsentOptions ?? new GoogleConsentOptions
        {
            ApiBaseUrl = "https://www.googleapis.com",
            AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
            TokenEndpoint = "https://oauth2.googleapis.com/token",
            Scope = "https://www.googleapis.com/auth/calendar.readonly"
        });
        return new GoogleCalendarSourceCore(
            httpClient,
            dbContext,
            instances,
            secretProtector,
            tokenClient,
            options,
            logger);
    }

    private static string SerializeSecret(
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

    private static GoogleCalendarSourceCore.GoogleSourceSecretData? DeserializeSecret(string? secretDataJson)
        => string.IsNullOrWhiteSpace(secretDataJson)
            ? null
            : JsonSerializer.Deserialize<GoogleCalendarSourceCore.GoogleSourceSecretData>(secretDataJson);

    private sealed class StubGoogleOAuthTokenClient : IGoogleOAuthTokenClient
    {
        public GoogleOAuthTokenResponse RefreshedToken { get; set; } =
            new("access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1));

        public int RefreshCallCount { get; private set; }

        public Task<GoogleOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string authorizationCode, string redirectUri, CancellationToken ct = default)
            => Task.FromResult(RefreshedToken);

        public Task<GoogleOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
        {
            RefreshCallCount++;
            return Task.FromResult(RefreshedToken);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class DelegatingHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}

