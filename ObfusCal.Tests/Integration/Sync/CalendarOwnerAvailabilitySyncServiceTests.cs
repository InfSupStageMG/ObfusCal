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

    private static CalendarOwnerAvailabilitySyncService CreateService(
        AppDbContext dbContext,
        ICalendarSource calendarSource,
        CapturingLogger<CalendarOwnerAvailabilitySyncService> logger)
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
            Options.Create(new SyncOptions
            {
                SyncIntervalSeconds = 900,
                LookAheadDays = 14
            }),
            scopeProvider.GetRequiredService<IServiceScopeFactory>(),
            logger);
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
}



