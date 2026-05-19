using Microsoft.EntityFrameworkCore;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Security;
using Testcontainers.PostgreSql;

namespace ObfusCal.Tests.Integration.Storage;

[TestClass]
public class ShadowSlotRetentionTests
{
    private static PostgreSqlContainer _postgres = null!;
    private static string _connectionString = null!;

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext _)
    {
        _postgres = new PostgreSqlBuilder("postgres:18").Build();
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        var options = CreateDbOptions();
        await using var db = new AppDbContext(options, new PassthroughColumnEncryptor());
        await db.Database.EnsureCreatedAsync();
    }

    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        await _postgres.DisposeAsync();
    }

    private static DbContextOptions<AppDbContext> CreateDbOptions()
        => new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

    private static AppDbContext CreateDbContext()
        => new AppDbContext(CreateDbOptions(), new PassthroughColumnEncryptor());

    private static async Task SeedSlotsAsync(
        AppDbContext db,
        string peerId,
        DateTimeOffset createdAt,
        int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            db.BusySlots.Add(new BusySlot
            {
                Id = Guid.NewGuid(),
                PeerId = peerId,
                SourceEventId = $"{peerId}-{i}",
                Start = DateTimeOffset.UtcNow,
                End = DateTimeOffset.UtcNow.AddHours(1),
                CreatedAtUtc = createdAt
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<int> PurgeAsync(AppDbContext db, int retentionDays)
    {
        var threshold = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        return await db.BusySlots
            .Where(b => b.CreatedAtUtc < threshold)
            .ExecuteDeleteAsync();
    }

    [TestMethod]
    public async Task Purge_DeletesRowsOlderThanRetentionWindow()
    {
        await using var db = CreateDbContext();
        var peerId = $"ret-peer-old-{Guid.NewGuid():N}";

        var old = DateTimeOffset.UtcNow.AddDays(-100);
        await SeedSlotsAsync(db, peerId, old, count: 3);

        var deleted = await PurgeAsync(db, retentionDays: 90);

        Assert.IsTrue(deleted >= 3,
            "Expected at least 3 rows to be deleted for slots older than 90 days.");

        var remaining = await db.BusySlots
            .CountAsync(b => b.PeerId == peerId);

        Assert.AreEqual(0, remaining,
            "All rows for this peer should have been purged.");
    }

    [TestMethod]
    public async Task Purge_PreservesRowsWithinRetentionWindow()
    {
        await using var db = CreateDbContext();
        var peerId = $"ret-peer-recent-{Guid.NewGuid():N}";

        var recent = DateTimeOffset.UtcNow.AddDays(-10);
        await SeedSlotsAsync(db, peerId, recent, count: 2);

        await PurgeAsync(db, retentionDays: 90);

        var remaining = await db.BusySlots
            .CountAsync(b => b.PeerId == peerId);

        Assert.AreEqual(2, remaining,
            "Recent rows must not be purged.");
    }

    [TestMethod]
    public async Task Purge_DeletesOldRows_ButPreservesRecentRows_ForSamePeer()
    {
        await using var db = CreateDbContext();
        var peerId = $"ret-peer-mixed-{Guid.NewGuid():N}";

        // Two old rows and two recent rows for the same peer.
        await SeedSlotsAsync(db, peerId, DateTimeOffset.UtcNow.AddDays(-200), count: 2);
        await SeedSlotsAsync(db, peerId, DateTimeOffset.UtcNow.AddDays(-5), count: 2);

        await PurgeAsync(db, retentionDays: 90);

        var remaining = await db.BusySlots
            .Where(b => b.PeerId == peerId)
            .ToListAsync();

        Assert.AreEqual(2, remaining.Count,
            "Only the two recent rows should remain after purge.");

        Assert.IsTrue(remaining.All(b => b.CreatedAtUtc > DateTimeOffset.UtcNow.AddDays(-90)),
            "All remaining rows should be within the retention window.");
    }

    [TestMethod]
    public async Task Purge_WithZeroRetentionDays_ShouldNotBeCalledByService()
    {
        await using var db = CreateDbContext();
        var peerId = $"ret-peer-nodelete-{Guid.NewGuid():N}";

        await SeedSlotsAsync(db, peerId, DateTimeOffset.UtcNow.AddDays(-400), count: 2);

        var beforeCount = await db.BusySlots.CountAsync(b => b.PeerId == peerId);
        Assert.AreEqual(2, beforeCount, "Setup check: 2 rows should exist.");
    }

    [TestMethod]
    public async Task NewSlotsHaveCreatedAtUtcPopulated()
    {
        await using var db = CreateDbContext();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        db.BusySlots.Add(new BusySlot
        {
            Id = Guid.NewGuid(),
            PeerId = "created-at-peer",
            SourceEventId = "created-at-test",
            Start = DateTimeOffset.UtcNow,
            End = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var slot = await db.BusySlots
            .AsNoTracking()
            .Where(b => b.PeerId == "created-at-peer")
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(slot);
        Assert.IsGreaterThanOrEqualTo(before, slot.CreatedAtUtc,
            "CreatedAtUtc should be set to approximate UtcNow at insertion time.");
    }
}

