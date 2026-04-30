using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

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
            Assert.AreEqual("https://graph.microsoft.com/v1.0/me/calendarView", request.RequestUri!.GetLeftPart(UriPartial.Path));
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
            RefreshedToken = new GraphOAuthTokenResponse("new-access-token", "new-refresh-token", DateTimeOffset.UtcNow.AddHours(1))
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
            new HttpClient(new DelegatingHttpMessageHandler(_ => throw new AssertFailedException("Graph endpoint should not be called.")))
            {
                BaseAddress = new Uri("https://graph.microsoft.com/")
            },
            tokenClient,
            logger,
            dataProtectionProvider);

        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var events = await source.GetEventsAsync(from, to, ownerId);

        Assert.AreEqual(0, events.Count);
        Assert.IsTrue(logger.Entries.Any(entry =>
            entry.LogLevel == LogLevel.Warning
            && entry.Message.Contains("Graph access token refresh failed", StringComparison.Ordinal)));
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
            RefreshedToken = new GraphOAuthTokenResponse("fresh-access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1))
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

    private static GraphCalendarSource CreateSource(
        AppDbContext dbContext,
        HttpClient httpClient,
        IGraphOAuthTokenClient tokenClient,
        ILogger<GraphCalendarSource> logger,
        IDataProtectionProvider dataProtectionProvider)
    {
        return new GraphCalendarSource(
            httpClient,
            dbContext,
            dataProtectionProvider,
            tokenClient,
            new MockCalendarSource(),
            new StubHostEnvironment(),
            logger);
    }

    private sealed class StubGraphOAuthTokenClient : IGraphOAuthTokenClient
    {
        public GraphOAuthTokenResponse RefreshedToken { get; set; } =
            new("access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1));

        public Exception? RefreshException { get; set; }
        public int RefreshCallCount { get; private set; }

        public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string authorizationCode, string redirectUri, CancellationToken ct = default)
            => Task.FromResult(RefreshedToken);

        public Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
        {
            RefreshCallCount++;
            return RefreshException is not null ? throw RefreshException : Task.FromResult(RefreshedToken);
        }
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "ObfusCal.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
