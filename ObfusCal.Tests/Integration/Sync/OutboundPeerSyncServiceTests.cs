using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Security;
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
        Assert.IsFalse(string.IsNullOrWhiteSpace(capturedRequest.PeerTimestampHeader));
        Assert.IsNull(capturedRequest.PinnedCertificateThumbprint);
        Assert.IsNull(capturedRequest.ClientCertificateThumbprint);

        using var document = JsonDocument.Parse(capturedRequest.Body);
        var root = document.RootElement;
        Assert.AreEqual(2, root.GetPropertyCount());
        Assert.AreEqual(calendarOwnerRef.ToString(), root.GetProperty("calendarOwnerRef").GetString());

        var slots = root.GetProperty("slots");
        Assert.AreEqual(JsonValueKind.Array, slots.ValueKind);
        Assert.AreEqual(1, slots.GetArrayLength());
        Assert.AreEqual(6, slots[0].GetPropertyCount());
        Assert.IsTrue(slots[0].TryGetProperty("start", out _));
        Assert.IsTrue(slots[0].TryGetProperty("end", out _));
        Assert.IsTrue(slots[0].TryGetProperty("title", out _));
        Assert.IsTrue(slots[0].TryGetProperty("description", out _));
        Assert.IsTrue(slots[0].TryGetProperty("attendeeEmails", out _));
        Assert.IsTrue(slots[0].TryGetProperty("location", out _));
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_UsesOwnerClientObfuscationProfile()
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

        var profileService = new StubCalendarOwnerObfuscationProfileService();
        await profileService.SetProfileAsync(
            calendarOwnerId,
            new ObfuscationProfileSettings(
                ObfuscationAuditContext.Client,
                RemoveTitle: true,
                RemoveDescription: true,
                RemoveLocation: true,
                RemoveAttendees: true,
                RoundTimes: false,
                RoundingIntervalMinutes: 15,
                MergeBlocks: true));

        var service = CreateService(
            dbContext,
            httpClientFactory,
            new StubCalendarSource([
                new CalendarEvent(
                    "event-1",
                    "Sensitive title",
                    "Sensitive description",
                    new DateTimeOffset(2026, 4, 29, 9, 7, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 29, 9, 37, 0, TimeSpan.Zero),
                    ["secret@example.test"],
                    "Sensitive location")
            ]),
            profileService: profileService);

        await service.RunSyncCycleAsync();

        Assert.IsNotNull(capturedRequest);
        using var document = JsonDocument.Parse(capturedRequest.Body);
        var slot = document.RootElement.GetProperty("slots")[0];
        Assert.AreEqual("2026-04-29T09:07:00+00:00", slot.GetProperty("start").GetString());
        Assert.AreEqual("2026-04-29T09:37:00+00:00", slot.GetProperty("end").GetString());
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_OnSuccess_RecordsLastSyncedAtAndSucceededOnPeerConnection()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        var peerConnectionId = SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-a", "https://peer-a.local/");

        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        var service = CreateService(dbContext, httpClientFactory, new StubCalendarSource([]));
        var beforeSync = DateTimeOffset.UtcNow;

        await service.RunSyncCycleAsync();

        var peer = await dbContext.PeerConnections.FindAsync([peerConnectionId]);
        Assert.IsNotNull(peer);
        Assert.IsTrue(peer.LastSyncSucceeded);
        Assert.IsNotNull(peer.LastSyncedAt);
        Assert.IsTrue(peer.LastSyncedAt >= beforeSync);
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_OnPeerHttpFailure_RecordsLastSyncedAtAndNotSucceededOnPeerConnection()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        var peerConnectionId = SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-a", "https://peer-a.local/");

        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))));

        var service = CreateService(dbContext, httpClientFactory, new StubCalendarSource([]));
        var beforeSync = DateTimeOffset.UtcNow;

        await service.RunSyncCycleAsync();

        var peer = await dbContext.PeerConnections.FindAsync([peerConnectionId]);
        Assert.IsNotNull(peer);
        Assert.IsFalse(peer.LastSyncSucceeded);
        Assert.IsNotNull(peer.LastSyncedAt);
        Assert.IsTrue(peer.LastSyncedAt >= beforeSync);
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
            logger: logger);

        await service.RunSyncCycleAsync();

        CollectionAssert.AreEquivalent(new[] { "peer-a.local", "peer-b.local" }, attemptedHosts);
        Assert.Contains(entry => entry.LogLevel == LogLevel.Warning, logger.Entries);
        Assert.Contains(entry => entry.LogLevel == LogLevel.Information && entry.Message.Contains("Successfully pushed"), logger.Entries);
    }

    private static OutboundPeerSyncService CreateService(
        AppDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ICalendarSource calendarSource,
        ICalendarOwnerObfuscationProfileService? profileService = null,
        ILogger<OutboundPeerSyncService>? logger = null,
        SyncOptions? options = null)
    {
        var pipeline = new ObfuscationPipeline([], [], NullLogger<ObfuscationPipeline>.Instance);
        var busySlotService = new CalendarOwnerClientBusySlotService(
            new FixedCalendarSourceResolver(calendarSource),
            pipeline,
            profileService ?? new StubCalendarOwnerObfuscationProfileService());

        return new OutboundPeerSyncService(
            dbContext,
            busySlotService,
            httpClientFactory,
            new FixedSyncRuntimeOptionsProvider(options ?? new SyncOptions
            {
                InstanceId = "local-instance-id",
                ApiKey = "local-instance",
                LookAheadDays = 14,
                SyncIntervalSeconds = 900
            }),
            logger ?? NullLogger<OutboundPeerSyncService>.Instance);
    }

    private sealed class FixedSyncRuntimeOptionsProvider(SyncOptions options) : ISyncRuntimeOptionsProvider
    {
        public SyncOptions Get() => options;
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_SkipsWhenOnlyInstanceIdIsConfigured()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, Guid.NewGuid(), "peer-a", "https://peer-a.local/");

        var httpRequestMade = false;
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
        {
            httpRequestMade = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        })));

        var service = CreateService(
            dbContext, httpClientFactory, new StubCalendarSource([]),
            options: new SyncOptions { InstanceId = "my-id", ApiKey = "", LookAheadDays = 14, SyncIntervalSeconds = 900 });

        await service.RunSyncCycleAsync();

        Assert.IsFalse(httpRequestMade, "Should skip sync when ApiKey is not configured");
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_SkipsWhenOnlyApiKeyIsConfigured()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, Guid.NewGuid(), "peer-a", "https://peer-a.local/");

        var httpRequestMade = false;
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
        {
            httpRequestMade = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        })));

        var service = CreateService(
            dbContext, httpClientFactory, new StubCalendarSource([]),
            options: new SyncOptions { InstanceId = "", ApiKey = "my-key", LookAheadDays = 14, SyncIntervalSeconds = 900 });

        await service.RunSyncCycleAsync();

        Assert.IsFalse(httpRequestMade, "Should skip sync when InstanceId is not configured");
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_WithZeroLookAheadDays_StillMakesRequest()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, Guid.NewGuid(), "peer-a", "https://peer-a.local/");

        var httpRequestMade = false;
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
        {
            httpRequestMade = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        })));

        var service = CreateService(
            dbContext, httpClientFactory, new StubCalendarSource([
                new CalendarEvent("e1", "Test", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), [], null)
            ]),
            options: new SyncOptions { InstanceId = "id", ApiKey = "key", LookAheadDays = 0, SyncIntervalSeconds = 900 });

        await service.RunSyncCycleAsync();

        Assert.IsTrue(httpRequestMade, "Should still sync with LookAheadDays=0 (clamped to 1)");
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_WithNoMappings_DoesNotMakeHttpRequest()
    {
        await using var dbContext = CreateDbContext();
        // Add a calendar owner but NO peer mappings
        dbContext.CalendarOwners.Add(new CalendarOwner { Id = Guid.NewGuid(), Name = "Orphan" });
        await dbContext.SaveChangesAsync();

        var httpRequestMade = false;
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
        {
            httpRequestMade = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        })));

        var service = CreateService(dbContext, httpClientFactory, new StubCalendarSource([]));

        await service.RunSyncCycleAsync();

        Assert.IsFalse(httpRequestMade, "No mappings = no HTTP requests");
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_WithInvalidBaseAddress_RecordsFailureAndContinues()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var peerConnectionId = SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, Guid.NewGuid(), "peer-bad", "http://peer-bad.local/");

        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        var service = CreateService(dbContext, httpClientFactory, new StubCalendarSource([]));

        await service.RunSyncCycleAsync();

        var peer = await dbContext.PeerConnections.FindAsync([peerConnectionId]);
        Assert.IsNotNull(peer);
        Assert.IsFalse(peer.LastSyncSucceeded, "Invalid base address should record failure");
    }

    [TestMethod]
    public async Task RunSyncCycleAsync_PropagatesPeerThumbprintsOnOutgoingRequest()
    {
        await using var dbContext = CreateDbContext();
        var calendarOwnerId = Guid.NewGuid();
        var calendarOwnerRef = Guid.NewGuid();
        var peerConnectionId = SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, "peer-pin", "https://peer-pin.local/");

        var peer = await dbContext.PeerConnections.FindAsync([peerConnectionId]);
        Assert.IsNotNull(peer);
        peer.PinnedCertificateThumbprint = "aa bb cc dd";
        peer.ClientCertificateThumbprint = "11 22 33 44";
        await dbContext.SaveChangesAsync();

        CapturedRequest? capturedRequest = null;
        var httpClientFactory = new StubHttpClientFactory(new HttpClient(new DelegatingHttpMessageHandler(async request =>
        {
            capturedRequest = await CapturedRequest.FromAsync(request);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })));

        var service = CreateService(dbContext, httpClientFactory, new StubCalendarSource([]));

        await service.RunSyncCycleAsync();

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual("AABBCCDD", capturedRequest.PinnedCertificateThumbprint);
        Assert.AreEqual("11223344", capturedRequest.ClientCertificateThumbprint);
    }

    private static AppDbContext CreateDbContext() => SyncIntegrationTestHelpers.CreateDbContext();

    private static Guid SeedOwnerAndPeerMapping(
        AppDbContext dbContext,
        Guid calendarOwnerId,
        Guid calendarOwnerRef,
        string peerInstanceId,
        string baseAddress)
        => SyncIntegrationTestHelpers.SeedOwnerAndPeerMapping(dbContext, calendarOwnerId, calendarOwnerRef, peerInstanceId, baseAddress);

    private sealed class StubCalendarSource(IReadOnlyList<CalendarEvent> events) : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? calendarOwnerId = null,
            CancellationToken ct = default)
            => Task.FromResult(events);
    }

    private sealed class FixedCalendarSourceResolver(ICalendarSource source) : ICalendarSourceResolver
    {
        public Task<ICalendarSource> ResolveAsync(Guid? calendarOwnerId = null, CancellationToken ct = default) =>
            Task.FromResult(source);
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string RequestUri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? PeerIdHeader,
        string? PeerTimestampHeader,
        string Body,
        string? PinnedCertificateThumbprint,
        string? ClientCertificateThumbprint)
    {
        public static async Task<CapturedRequest> FromAsync(HttpRequestMessage request)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync();

            var peerIdHeader = request.Headers.TryGetValues("X-Peer-Id", out var values)
                ? values.SingleOrDefault()
                : null;

            var peerTimestampHeader = request.Headers.TryGetValues("X-Peer-Timestamp", out var timestampValues)
                ? timestampValues.SingleOrDefault()
                : null;

            var pinnedThumbprint = request.Options.TryGetValue(PeerTransportRequestOptions.PinnedCertificateThumbprint, out var pinned)
                ? PeerTransportSecurity.NormalizeThumbprint(pinned)
                : null;

            var clientThumbprint = request.Options.TryGetValue(PeerTransportRequestOptions.ClientCertificateThumbprint, out var client)
                ? PeerTransportSecurity.NormalizeThumbprint(client)
                : null;

            return new CapturedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                peerIdHeader,
                peerTimestampHeader,
                body,
                pinnedThumbprint,
                clientThumbprint);
        }
    }

    private sealed class StubCalendarOwnerObfuscationProfileService : ICalendarOwnerObfuscationProfileService
    {
        private readonly Dictionary<(Guid OwnerId, ObfuscationAuditContext Context), ObfuscationProfileSettings> _profiles = new();

        public Task<IReadOnlyList<ObfuscationProfileSettings>> GetProfilesAsync(Guid calendarOwnerId, CancellationToken ct = default)
        {
            var result = Enum.GetValues<ObfuscationAuditContext>()
                .Select(context => _profiles.TryGetValue((calendarOwnerId, context), out var profile)
                    ? profile
                    : ObfuscationProfileSettings.CreateDefault(context))
                .ToList();
            return Task.FromResult<IReadOnlyList<ObfuscationProfileSettings>>(result);
        }

        public Task<ObfuscationProfileSettings> GetProfileAsync(Guid calendarOwnerId, ObfuscationAuditContext context, CancellationToken ct = default)
        {
            return Task.FromResult(_profiles.TryGetValue((calendarOwnerId, context), out var profile)
                ? profile
                : ObfuscationProfileSettings.CreateDefault(context));
        }

        public Task<ObfuscationProfileSettings> SetProfileAsync(Guid calendarOwnerId, ObfuscationProfileSettings profile, CancellationToken ct = default)
        {
            _profiles[(calendarOwnerId, profile.Context)] = profile;
            return Task.FromResult(profile);
        }
    }
}
