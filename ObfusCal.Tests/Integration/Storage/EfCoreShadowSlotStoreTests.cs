using Microsoft.EntityFrameworkCore;
using ObfusCal.Infrastructure.Persistence;
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
        _postgres = new PostgreSqlBuilder("postgres:17")
            .Build();

        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        // Apply schema
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        await using var db = new AppDbContext(options);
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
        return new AppDbContext(options);
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
}
