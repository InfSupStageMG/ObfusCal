using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Sync;

namespace ObfusCal.Tests.Integration.Sync;

[TestClass]
public class OutboundPeerSyncServiceTests
{
    [TestMethod]
    public async Task RunSyncCycleAsync_PostsCalendarOwnerRefAndBusySlotsWithPeerHeaders()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-a", "https://peer-a.local/");

        CapturedRequest? capturedRequest = null;
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(async request =>
        {
            capturedRequest = await CapturedRequest.FromAsync(request);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        })));

        var service = CreateService(
            dbContext,
            httpClientFactory,
            new StubCalendarSource([
                new CalendarEvent(
                    "event-1",
                    "Sensitive title",
                    "Sensitive description",
                    new DateTimeOffset(2026, 4, 29, 9, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 29, 10, 0, 0, TimeSpan.Zero),
                    ["secret@example.test"],
                    "Sensitive location")
            ]));

        await service.RunSyncCycleAsync();

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
        Assert.AreEqual("https://peer-a.local/api/shadow-slots", capturedRequest.RequestUri);
        Assert.AreEqual("ApiKey", capturedRequest.AuthorizationScheme);
        Assert.AreEqual("local-instance", capturedRequest.AuthorizationParameter);
        Assert.AreEqual("local-instance-id", capturedRequest.PeerIdHeader);

        using var document = JsonDocument.Parse(capturedRequest.Body);
        var root = document.RootElement;
        Assert.AreEqual(2, root.GetPropertyCount());
        Assert.AreEqual(calendarOwnerRef.ToString(), root.GetProperty("calendarOwnerRef").GetString());

        var slots = root.GetProperty("slots");
        Assert.AreEqual(JsonValueKind.Array, slots.ValueKind);
        Assert.AreEqual(1, slots.GetArrayLength());
        Assert.AreEqual(2, slots[0].GetPropertyCount());
        Assert.IsTrue(slots[0].TryGetProperty("start", out _));
        Assert.IsTrue(slots[0].TryGetProperty("end", out _));
        Assert.IsFalse(slots[0].TryGetProperty("title", out _));
        Assert.IsFalse(slots[0].TryGetProperty("description", out _));
        Assert.IsFalse(slots[0].TryGetProperty("attendeeEmails", out _));
        Assert.IsFalse(slots[0].TryGetProperty("location", out _));
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_WhenOnePeerFails_ContinuesWithRemainingPeersAndLogsWarning()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, Guid.NewGuid(), "peer-a", "https://peer-a.local/");
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, Guid.NewGuid(), "peer-b", "https://peer-b.local/");

        var logger = new CapturingLogger<OutboundPeerSyncService>();
        var attemptedHosts = new List<string>();
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(request =>
        {
            attemptedHosts.Add(request.RequestUri!.Host);
            return Task.FromResult(request.RequestUri!.Host == "peer-a.local"
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK));
        })));

        var service = CreateService(
            dbContext,
            httpClientFactory,
            new StubCalendarSource([
                new CalendarEvent(
                    "event-1",
                    "Busy",
                    null,
                    new DateTimeOffset(2026, 4, 29, 9, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 29, 10, 0, 0, TimeSpan.Zero),
                    [],
                    null)
            ]),
            logger);

        await service.RunSyncCycleAsync();

        CollectionAssert.AreEquivalent(new[] { "peer-a.local", "peer-b.local" }, attemptedHosts);
        Assert.Contains(entry => entry.LogLevel == LogLevel.Warning, logger.Entries);
        Assert.Contains(entry => entry.LogLevel == LogLevel.Information && entry.Message.Contains("Successfully pushed"), logger.Entries);
    }

    private static OutboundPeerSyncService CreateService(
        AppDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ICalendarSource calendarSource,
        ILogger<OutboundPeerSyncService>? logger = null)
    {
        var pipeline = new ObfuscationPipeline([], [], new NullLogger<ObfuscationPipeline>());

        return new OutboundPeerSyncService(
            dbContext,
            calendarSource,
            pipeline,
            httpClientFactory,
            Options.Create(new SyncOptions
            {
                InstanceId = "local-instance-id",
                ApiKey = "local-instance",
                LookAheadDays = 14,
                SyncIntervalSeconds = 900
            }),
            logger ?? new NullLogger<OutboundPeerSyncService>());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static void SeedOwnerAndPeerMapping(
        AppDbContext dbContext,
        Guid calendarOwnerId,
        Guid calendarOwnerRef,
        string peerInstanceId,
        string baseAddress)
    {
        if (!dbContext.CalendarOwners.Any(owner => owner.Id == calendarOwnerId))
        {
            dbContext.CalendarOwners.Add(new CalendarOwner
            {
                Id = calendarOwnerId,
                Name = "Owner"
            });
        }

        var peerConnectionId = Guid.NewGuid();
        dbContext.PeerConnections.Add(new PeerConnection
        {
            Id = peerConnectionId,
            InstanceId = peerInstanceId,
            BaseAddress = baseAddress,
            ApiKeyHash = "hashed-not-used-here"
        });

        dbContext.CalendarOwnerPeerMappings.Add(new CalendarOwnerPeerMapping
        {
            Id = Guid.NewGuid(),
            CalendarOwnerId = calendarOwnerId,
            PeerConnectionId = peerConnectionId,
            CalendarOwnerRef = calendarOwnerRef
        });

        dbContext.SaveChanges();
    }

    private sealed class StubCalendarSource(IReadOnlyList<CalendarEvent> events) : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? calendarOwnerId = null,
            CancellationToken ct = default)
            => Task.FromResult(events);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class DelegatingHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string RequestUri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? PeerIdHeader,
        string Body)
    {
        public static async Task<CapturedRequest> FromAsync(HttpRequestMessage request)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync();

            var peerIdHeader = request.Headers.TryGetValues("X-Peer-Id", out var values)
                ? values.SingleOrDefault()
                : null;

            return new CapturedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                peerIdHeader,
                body);
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

    private sealed class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}


