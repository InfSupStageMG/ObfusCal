using ObfusCal.Core.Models;
using ObfusCal.Infrastructure.Storage;

namespace ObfusCal.Tests;

[TestClass]
public class InMemoryShadowSlotStoreTests
{
    [TestMethod]
    public void SetSlots_ThenGetSlots_ReturnsSavedSlotsForPeer()
    {
        var store = new InMemoryShadowSlotStore();
        var slots = new[]
        {
            new BusySlot("evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))
        };

        store.SetSlots("peer-a", slots);
        var retrieved = store.GetSlots("peer-a");

        Assert.HasCount(1, retrieved);
        Assert.AreEqual("evt-1", retrieved[0].SourceEventId);
    }

    [TestMethod]
    public void SetSlots_ForDifferentPeers_KeepsDataIsolated()
    {
        var store = new InMemoryShadowSlotStore();

        store.SetSlots("peer-a", [new BusySlot("a-evt", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30))]);
        store.SetSlots("peer-b", [new BusySlot("b-evt", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(45))]);

        var peerASlots = store.GetSlots("peer-a");
        var peerBSlots = store.GetSlots("peer-b");

        Assert.AreEqual("a-evt", peerASlots[0].SourceEventId);
        Assert.AreEqual("b-evt", peerBSlots[0].SourceEventId);
    }

    [TestMethod]
    public async Task SetSlots_AndGetSlots_AreThreadSafeUnderConcurrentAccess()
    {
        var store = new InMemoryShadowSlotStore();

        var tasks = Enumerable.Range(0, 400)
            .Select(i => Task.Run(() =>
            {
                var peerId = $"peer-{i % 20}";
                var slot = new BusySlot($"evt-{i}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15));

                store.SetSlots(peerId, [slot]);
                _ = store.GetSlots(peerId);
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        for (var i = 0; i < 20; i++)
        {
            var result = store.GetSlots($"peer-{i}");
            Assert.HasCount(1, result);
        }
    }
}
