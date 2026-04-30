using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    [TestMethod]
    public async Task RunSyncCycleAsync_OnSuccess_RecordsLastSyncedAtAndSucceededOnPeerConnection()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        var peerConnectionId = SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-a", "https://peer-a.local/");

        var store = new EfCoreShadowSlotStore(dbContext, Serilog.Core.Logger.None);
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            }))));

        var service = CreateService(dbContext, store, httpClientFactory);
        var beforeSync = DateTimeOffset.UtcNow;

        await service.RunSyncCycleAsync();

        var peer = await dbContext.PeerConnections.FindAsync([peerConnectionId]);
        Assert.IsNotNull(peer);
        Assert.IsTrue(peer.LastSyncSucceeded);
        Assert.IsNotNull(peer.LastSyncedAt);
        Assert.IsTrue(peer.LastSyncedAt >= beforeSync);
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_OnHttpFailure_RecordsLastSyncedAtAndNotSucceededOnPeerConnection()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        var peerConnectionId = SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-a", "https://peer-a.local/");

        var store = new EfCoreShadowSlotStore(dbContext, Serilog.Core.Logger.None);
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))));

        var service = CreateService(dbContext, store, httpClientFactory);
        var beforeSync = DateTimeOffset.UtcNow;

        await service.RunSyncCycleAsync();

        var peer = await dbContext.PeerConnections.FindAsync([peerConnectionId]);
        Assert.IsNotNull(peer);
        Assert.IsFalse(peer.LastSyncSucceeded);
        Assert.IsNotNull(peer.LastSyncedAt);
        Assert.IsTrue(peer.LastSyncedAt >= beforeSync);
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_WhenPeerAFails_StillPullsFromPeerBAndLogsWarning()
    {
        await using var dbContext = CreateDbContext();

        var ownerAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, ownerAId, Guid.NewGuid(), "peer-a", "https://peer-a.local/");
        SeedOwnerAndPeerMapping(dbContext, ownerBId, Guid.NewGuid(), "peer-b", "https://peer-b.local/");

        var store = new EfCoreShadowSlotStore(dbContext, Serilog.Core.Logger.None);
        var logger = new CapturingLogger<InboundPeerPullSyncService>();

        var attemptedHosts = new List<string>();
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(request =>
        {
            attemptedHosts.Add(request.RequestUri!.Host);
            return Task.FromResult(request.RequestUri!.Host == "peer-a.local"
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        })));

        var service = CreateService(dbContext, store, httpClientFactory, logger);

        await service.RunSyncCycleAsync();

        CollectionAssert.AreEquivalent(new[] { "peer-a.local", "peer-b.local" }, attemptedHosts);
        Assert.Contains(entry => entry.LogLevel == LogLevel.Warning, logger.Entries);
        Assert.Contains(entry => entry.LogLevel == LogLevel.Information && entry.Message.Contains("Successfully pulled"), logger.Entries);
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_SkipsWhenOnlyInstanceIdIsConfigured()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, Guid.NewGuid(), "peer-a", "https://peer-a.local/");

        var store = new EfCoreShadowSlotStore(dbContext, Serilog.Core.Logger.None);
        var httpRequestMade = false;
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
        {
            httpRequestMade = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        })));

        var service = new InboundPeerPullSyncService(
            dbContext, store, httpClientFactory,
            Options.Create(new SyncOptions
            {
                InstanceId = "configured-id",
                ApiKey = "", // empty = not configured
                LookAheadDays = 14,
                SyncIntervalSeconds = 900
            }),
            NullLogger<InboundPeerPullSyncService>.Instance);

        await service.RunSyncCycleAsync();

        Assert.IsFalse(httpRequestMade, "Should skip sync when ApiKey is not configured");
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_SkipsWhenOnlyApiKeyIsConfigured()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, Guid.NewGuid(), "peer-a", "https://peer-a.local/");

        var store = new EfCoreShadowSlotStore(dbContext, Serilog.Core.Logger.None);
        var httpRequestMade = false;
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
        {
            httpRequestMade = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        })));

        var service = new InboundPeerPullSyncService(
            dbContext, store, httpClientFactory,
            Options.Create(new SyncOptions
            {
                InstanceId = "", // empty = not configured
                ApiKey = "configured-key",
                LookAheadDays = 14,
                SyncIntervalSeconds = 900
            }),
            NullLogger<InboundPeerPullSyncService>.Instance);

        await service.RunSyncCycleAsync();

        Assert.IsFalse(httpRequestMade, "Should skip sync when InstanceId is not configured");
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_WithZeroLookAheadDays_ClampsToOne()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-a", "https://peer-a.local/");

        var store = new EfCoreShadowSlotStore(dbContext, Serilog.Core.Logger.None);
        string? capturedUri = null;
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(request =>
        {
            capturedUri = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        })));

        var service = new InboundPeerPullSyncService(
            dbContext, store, httpClientFactory,
            Options.Create(new SyncOptions
            {
                InstanceId = "my-id",
                ApiKey = "my-key",
                LookAheadDays = 0, // should be clamped to at least 1
                SyncIntervalSeconds = 900
            }),
            NullLogger<InboundPeerPullSyncService>.Instance);

        await service.RunSyncCycleAsync();

        Assert.IsNotNull(capturedUri, "Should have made an HTTP request");
        Assert.IsTrue(capturedUri.Contains("to="), "Request URI should contain 'to' parameter");
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_WithInvalidPeerBaseAddress_RecordsFailure()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        var peerConnectionId = SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-bad", "not a valid url");

        var store = new EfCoreShadowSlotStore(dbContext, Serilog.Core.Logger.None);
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }))));

        var service = CreateService(dbContext, store, httpClientFactory);

        await service.RunSyncCycleAsync();

        var peer = await dbContext.PeerConnections.FindAsync([peerConnectionId]);
        Assert.IsNotNull(peer);
        Assert.IsFalse(peer.LastSyncSucceeded, "Invalid base address should record failure");
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
            logger ?? NullLogger<InboundPeerPullSyncService>.Instance);
    }

    private static AppDbContext CreateDbContext() => SyncIntegrationTestHelpers.CreateDbContext();

    private static Guid SeedOwnerAndPeerMapping(
        AppDbContext dbContext,
        Guid calendarOwnerId,
        Guid calendarOwnerRef,
        string peerInstanceId,
        string baseAddress)
        => SyncIntegrationTestHelpers.SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, peerInstanceId, baseAddress);

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
}
