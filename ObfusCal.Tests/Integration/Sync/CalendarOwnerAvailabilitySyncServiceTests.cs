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

            Assert.AreEqual(1, requests.Count(entry => entry.Method == HttpMethod.Delete));
            Assert.IsTrue(requests.Any(entry => entry.Method == HttpMethod.Delete && entry.Uri.Contains("managed-1", StringComparison.Ordinal)));
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
        var source = CreateGraphSource(
            dbContext,
            dataProtectionProvider,
            request =>
            {
                requests.Add((request.Method, request.RequestUri!.AbsolutePath));
                return Task.FromResult(HttpClientHandlerStub.JsonResponse("{\"value\":[]}"));
            });

        var service = CreateService(
            dbContext,
            source,
            new CapturingLogger<CalendarOwnerAvailabilitySyncService>(),
            new StubShadowSlotStore([new BusySlot("peer-slot-1", DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddMinutes(40))]));

        await service.RunSyncForOwnerAsync(ownerId);

        Assert.AreEqual(1, requests.Count, "Only the normal calendar read should run when write-back is disabled.");
        Assert.AreEqual("/v1.0/me/calendarView", requests[0].Path);
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

    private static GraphCalendarSource CreateGraphSource(
        AppDbContext dbContext,
        IDataProtectionProvider dataProtectionProvider,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var instances = new FakeCalendarSourceInstanceService(ownerId => dbContext.CalendarOwners.Any(owner => owner.Id == ownerId));
        var messageHandler = new DelegatingHttpMessageHandler(handler);
        var httpClient = new HttpClient(messageHandler, disposeHandler: true)
        {
            BaseAddress = new Uri("https://graph.microsoft.com/")
        };

        return new GraphCalendarSource(
            httpClient,
            dbContext,
            dataProtectionProvider,
            new StubGraphOAuthTokenClient(),
            instances,
            NullLogger<GraphCalendarSource>.Instance);
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
        public Task<GraphOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string authorizationCode, string redirectUri, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<GraphOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
            => Task.FromResult(new GraphOAuthTokenResponse("access-token", refreshToken, DateTimeOffset.UtcNow.AddHours(1)));
    }

    private sealed class DelegatingHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
