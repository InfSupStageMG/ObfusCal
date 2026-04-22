using ObfusCal.Core.Models;
using ObfusCal.Infrastructure.Storage;

namespace ObfusCal.Tests;

[TestClass]
public class InMemoryShadowSlotStoreTests
{
    [TestMethod]
    public async Task SetSlotsAsync_ThenGetSlotsAsync_ReturnsSavedSlotsForPeer()
    {
        var store = new InMemoryShadowSlotStore();
        var slots = new[]
        {
            new BusySlot("evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))
        };

        await store.SetSlotsAsync("peer-a", slots);
        var retrieved = await store.GetSlotsAsync("peer-a");

        Assert.HasCount(1, retrieved);
        Assert.AreEqual("evt-1", retrieved[0].SourceEventId);
    }

    [TestMethod]
    public async Task SetSlotsAsync_ForDifferentPeers_KeepsDataIsolated()
    {
        var store = new InMemoryShadowSlotStore();

        await store.SetSlotsAsync("peer-a", [new BusySlot("a-evt", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30))]);
        await store.SetSlotsAsync("peer-b", [new BusySlot("b-evt", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(45))]);

        var peerASlots = await store.GetSlotsAsync("peer-a");
        var peerBSlots = await store.GetSlotsAsync("peer-b");

        Assert.AreEqual("a-evt", peerASlots[0].SourceEventId);
        Assert.AreEqual("b-evt", peerBSlots[0].SourceEventId);
    }

    [TestMethod]
    public async Task SetSlotsAsync_AndGetSlotsAsync_AreThreadSafeUnderConcurrentAccess()
    {
        var store = new InMemoryShadowSlotStore();

        var tasks = Enumerable.Range(0, 400)
            .Select(i => Task.Run(async () =>
            {
                var peerId = $"peer-{i % 20}";
                var slot = new BusySlot($"evt-{i}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15));
                await store.SetSlotsAsync(peerId, [slot]);
                _ = await store.GetSlotsAsync(peerId);
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        for (var i = 0; i < 20; i++)
        {
            var result = await store.GetSlotsAsync($"peer-{i}");
            Assert.HasCount(1, result);
        }
    }
}
