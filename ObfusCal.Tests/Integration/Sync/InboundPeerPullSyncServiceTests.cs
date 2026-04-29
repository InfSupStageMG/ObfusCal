using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Storage;
using ObfusCal.Infrastructure.Sync;
using CoreBusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Tests.Integration.Sync;

[TestClass]
public class InboundPeerPullSyncServiceTests
{
    [TestMethod]
    public async Task RunSyncCycleAsync_OnSuccess_ReplacesOwnerScopedSlotsAndSendsPeerHeaders()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-a", "https://peer-a.local/");

        var store = new EfCoreShadowSlotStore(dbContext, Serilog.Core.Logger.None);
        await store.SetSlotsAsync("peer-a", calendarOwnerId,
        [
            new CoreBusySlot("old", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15))
        ]);

        CapturedRequest? capturedRequest = null;
        var pulledSlots = new[]
        {
            new { start = new DateTimeOffset(2026, 4, 29, 9, 0, 0, TimeSpan.Zero), end = new DateTimeOffset(2026, 4, 29, 10, 0, 0, TimeSpan.Zero) },
            new { start = new DateTimeOffset(2026, 4, 30, 9, 0, 0, TimeSpan.Zero), end = new DateTimeOffset(2026, 4, 30, 10, 0, 0, TimeSpan.Zero) }
        };

        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(async request =>
        {
            capturedRequest = await CapturedRequest.FromAsync(request);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(pulledSlots))
            };
        })));

        var service = CreateService(dbContext, store, httpClientFactory);

        await service.RunSyncCycleAsync();

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Get, capturedRequest.Method);
        Assert.AreEqual("ApiKey", capturedRequest.AuthorizationScheme);
        Assert.AreEqual("local-instance", capturedRequest.AuthorizationParameter);
        Assert.AreEqual("local-instance-id", capturedRequest.PeerIdHeader);
        Assert.StartsWith($"https://peer-a.local/api/sync/busy-slots/{calendarOwnerRef}?", capturedRequest.RequestUri);
        Assert.Contains("from=", capturedRequest.RequestUri);
        Assert.Contains("to=", capturedRequest.RequestUri);

        var storedSlots = await store.GetSlotsAsync("peer-a", calendarOwnerId);
        Assert.HasCount(2, storedSlots);
        Assert.DoesNotContain(slot => slot.SourceEventId == "old", storedSlots);
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_OnFailure_PreservesPreviouslyStoredSlots()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-a", "https://peer-a.local/");

        var store = new EfCoreShadowSlotStore(dbContext, Serilog.Core.Logger.None);
        var existingSlot = new CoreBusySlot("existing", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30));
        await store.SetSlotsAsync("peer-a", calendarOwnerId, [existingSlot]);

        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)))));

        var service = CreateService(dbContext, store, httpClientFactory);

        await service.RunSyncCycleAsync();

        var storedSlots = await store.GetSlotsAsync("peer-a", calendarOwnerId);
        Assert.HasCount(1, storedSlots);
        Assert.AreEqual("existing", storedSlots[0].SourceEventId);
    }

    private static InboundPeerPullSyncService CreateService(
        AppDbContext dbContext,
        EfCoreShadowSlotStore store,
        IHttpClientFactory httpClientFactory,
        ILogger<InboundPeerPullSyncService>? logger = null)
    {
        return new InboundPeerPullSyncService(
            dbContext,
            store,
            httpClientFactory,
            Options.Create(new SyncOptions
            {
                InstanceId = "local-instance-id",
                ApiKey = "local-instance",
                LookAheadDays = 14,
                SyncIntervalSeconds = 900
            }),
            logger ?? new NullLogger<InboundPeerPullSyncService>());
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
        dbContext.CalendarOwners.Add(new CalendarOwner
        {
            Id = calendarOwnerId,
            Name = "Owner"
        });

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
        string? PeerIdHeader)
    {
        public static Task<CapturedRequest> FromAsync(HttpRequestMessage request)
        {
            var peerIdHeader = request.Headers.TryGetValues("X-Peer-Id", out var values)
                ? values.SingleOrDefault()
                : null;

            return Task.FromResult(new CapturedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                peerIdHeader));
        }
    }

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

