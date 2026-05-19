using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Security;
using ObfusCal.Infrastructure.Storage;
using Testcontainers.PostgreSql;
using BusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Tests.Integration.Storage;

[TestClass]
public class EfCoreShadowSlotStoreTests
{
    private static PostgreSqlContainer _postgres = null!;
    private static string _connectionString = null!;

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        _postgres = new PostgreSqlBuilder("postgres:18")
            .Build();

        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        // Apply schema
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        await using var db = new AppDbContext(options, new PassthroughColumnEncryptor());
        await db.Database.EnsureCreatedAsync();
    }

    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        await _postgres.DisposeAsync();
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new AppDbContext(options, new PassthroughColumnEncryptor());
    }

    [TestMethod]
    public async Task SetSlotsAsync_ThenGetSlotsAsync_ReturnsSavedSlotsForPeer()
    {
        await using var db = CreateDbContext();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        var slots = new[]
        {
            new BusySlot("ef-evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))
        };

        await store.SetSlotsAsync("ef-peer-a", slots);
        var retrieved = await store.GetSlotsAsync("ef-peer-a");

        Assert.HasCount(1, retrieved);
        Assert.AreEqual("ef-evt-1", retrieved[0].SourceEventId);
    }

    [TestMethod]
    public async Task SetSlotsAsync_ReplacesExistingSlots_ForSamePeer()
    {
        await using var db = CreateDbContext();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        await store.SetSlotsAsync("ef-peer-replace",
            [new BusySlot("old-evt", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        await store.SetSlotsAsync("ef-peer-replace", [
            new BusySlot("new-evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2)),
            new BusySlot("new-evt-2", DateTimeOffset.UtcNow.AddHours(3), DateTimeOffset.UtcNow.AddHours(4))
        ]);

        var result = await store.GetSlotsAsync("ef-peer-replace");

        Assert.HasCount(2, result);
        Assert.Contains(s => s.SourceEventId == "new-evt-1", result);
        Assert.Contains(s => s.SourceEventId == "new-evt-2", result);
        Assert.DoesNotContain(s => s.SourceEventId == "old-evt", result);
    }

    [TestMethod]
    public async Task GetSlotsAsync_ReturnsEmpty_WhenNothingStoredForPeer()
    {
        await using var db = CreateDbContext();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        var result = await store.GetSlotsAsync("ef-peer-unknown");

        Assert.HasCount(0, result);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ReturnsSlotsAcrossAllPeers()
    {
        await using var db = CreateDbContext();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        await store.SetSlotsAsync("ef-get-all-a",
            [new BusySlot("ga-evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);
        await store.SetSlotsAsync("ef-get-all-b",
            [new BusySlot("ga-evt-2", DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(3))]);

        var allSlots = await store.GetAllSlotsAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.Contains(s => s.SourceEventId == "ga-evt-1", allSlots);
        Assert.Contains(s => s.SourceEventId == "ga-evt-2", allSlots);
    }

    [TestMethod]
    public async Task SetSlotsAsync_WithOwnerScope_ReplacesOnlyThatOwnerScope()
    {
        await using var db = CreateDbContext();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        await store.SetSlotsAsync("ef-peer-owner", ownerA,
            [new BusySlot("old", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);
        await store.SetSlotsAsync("ef-peer-owner", ownerB,
            [new BusySlot("other", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        await store.SetSlotsAsync("ef-peer-owner", ownerA,
            [new BusySlot("new", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2))]);

        var ownerASlots = await store.GetSlotsAsync("ef-peer-owner", ownerA);
        var ownerBSlots = await store.GetSlotsAsync("ef-peer-owner", ownerB);

        Assert.HasCount(1, ownerASlots);
        Assert.AreEqual("new", ownerASlots[0].SourceEventId);
        Assert.HasCount(1, ownerBSlots);
        Assert.AreEqual("other", ownerBSlots[0].SourceEventId);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_WithOwnerScope_ReturnsOnlyMatchingOwner()
    {
        await using var db = CreateDbContext();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        await SeedOwnerPeerMappingAsync(db, ownerA, "ef-owner-scope-peer-a");
        await SeedOwnerPeerMappingAsync(db, ownerB, "ef-owner-scope-peer-b");

        await store.SetSlotsAsync("ef-owner-scope-peer-a", ownerA,
            [new BusySlot("a-scope", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);
        await store.SetSlotsAsync("ef-owner-scope-peer-b", ownerB,
            [new BusySlot("b-scope", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        var result = await store.GetAllSlotsAsync(ownerA, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.HasCount(1, result);
        Assert.AreEqual("a-scope", result[0].SourceEventId);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_WithOwnerScope_ExcludesSlotsWhenNoActivePeerRelationshipExists()
    {
        await using var db = CreateDbContext();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var stalePeerId = "ef-stale-peer";

        await store.SetSlotsAsync(stalePeerId, ownerId,
            [new BusySlot("stale", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        var withoutMapping = await store.GetAllSlotsAsync(ownerId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        Assert.HasCount(0, withoutMapping);

        await SeedOwnerPeerMappingAsync(db, ownerId, stalePeerId, PeerConnectionStatus.Suspended);
        var withSuspendedMapping = await store.GetAllSlotsAsync(ownerId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        Assert.HasCount(0, withSuspendedMapping);

        await SeedOwnerPeerMappingAsync(db, ownerId, stalePeerId, PeerConnectionStatus.Active);
        var withActiveMapping = await store.GetAllSlotsAsync(ownerId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        Assert.HasCount(1, withActiveMapping);
        Assert.AreEqual("stale", withActiveMapping[0].SourceEventId);
    }

    private static async Task SeedOwnerPeerMappingAsync(
        AppDbContext db,
        Guid ownerId,
        string peerInstanceId,
        PeerConnectionStatus status = PeerConnectionStatus.Active)
    {
        if (!await db.CalendarOwners.AnyAsync(owner => owner.Id == ownerId))
        {
            db.CalendarOwners.Add(new CalendarOwner
            {
                Id = ownerId,
                Name = "Integration Owner"
            });
        }

        var existingPeer = await db.PeerConnections.SingleOrDefaultAsync(peer => peer.InstanceId == peerInstanceId);
        var peerId = existingPeer?.Id ?? Guid.NewGuid();

        if (existingPeer is null)
        {
            db.PeerConnections.Add(new PeerConnection
            {
                Id = peerId,
                InstanceId = peerInstanceId,
                BaseAddress = "https://peer.test/",
                ApiKeyHash = "hash",
                Status = status
            });
        }
        else
        {
            existingPeer.Status = status;
        }

        var existingMapping = await db.CalendarOwnerPeerMappings
            .SingleOrDefaultAsync(mapping => mapping.CalendarOwnerId == ownerId && mapping.PeerConnectionId == peerId);

        if (existingMapping is null)
        {
            db.CalendarOwnerPeerMappings.Add(new CalendarOwnerPeerMapping
            {
                Id = Guid.NewGuid(),
                CalendarOwnerId = ownerId,
                PeerConnectionId = peerId,
                CalendarOwnerRef = Guid.NewGuid()
            });
        }

        await db.SaveChangesAsync();
    }
}
