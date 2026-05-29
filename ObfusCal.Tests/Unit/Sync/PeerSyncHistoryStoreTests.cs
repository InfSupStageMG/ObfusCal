using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Sync;

[TestClass]
public class PeerSyncHistoryStoreTests
{
    [TestMethod]
    public async Task GetLastCompletedAtUtcAsync_ReturnsNull_WhenNoStateExists()
    {
        using var dbContext = TestDbContextFactory.CreateInMemory();
        var store = new EfCorePeerSyncHistoryStore(dbContext);

        var lastCompletedAtUtc = await store.GetLastCompletedAtUtcAsync();

        Assert.IsNull(lastCompletedAtUtc);
    }

    [TestMethod]
    public async Task SetLastCompletedAtUtcAsync_PersistsTimestamp()
    {
        using var dbContext = TestDbContextFactory.CreateInMemory();
        var store = new EfCorePeerSyncHistoryStore(dbContext);
        var expected = DateTimeOffset.UtcNow;

        await store.SetLastCompletedAtUtcAsync(expected);
        var actual = await store.GetLastCompletedAtUtcAsync();

        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.ToUniversalTime(), actual.Value);
    }

    [TestMethod]
    public async Task SetLastCompletedAtUtcAsync_DoesNotMoveTimestampBackwards()
    {
        using var dbContext = TestDbContextFactory.CreateInMemory();
        var store = new EfCorePeerSyncHistoryStore(dbContext);
        var newest = DateTimeOffset.UtcNow;
        var older = newest.AddMinutes(-5);

        await store.SetLastCompletedAtUtcAsync(newest);
        await store.SetLastCompletedAtUtcAsync(older);

        var actual = await store.GetLastCompletedAtUtcAsync();

        Assert.IsNotNull(actual);
        Assert.AreEqual(newest.ToUniversalTime(), actual.Value);
    }
}

