using ObfusCal.Core.Models;
using ObfusCal.Infrastructure.Storage;

namespace ObfusCal.Tests.Unit.Storage;

[TestClass]
public class InMemoryShadowSlotStoreTests
{
    [TestMethod]
    public async Task SetSlotsAsync_ThenGetSlotsAsync_ReturnsSavedSlotsForPeer()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
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
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);

        await store.SetSlotsAsync("peer-a",
            [new BusySlot("a-evt", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30))]);
        await store.SetSlotsAsync("peer-b",
            [new BusySlot("b-evt", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(45))]);

        var peerASlots = await store.GetSlotsAsync("peer-a");
        var peerBSlots = await store.GetSlotsAsync("peer-b");

        Assert.AreEqual("a-evt", peerASlots[0].SourceEventId);
        Assert.AreEqual("b-evt", peerBSlots[0].SourceEventId);
    }

    [TestMethod]
    public async Task SetSlotsAsync_AndGetSlotsAsync_AreThreadSafeUnderConcurrentAccess()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);

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

    [TestMethod]
    public async Task GetAllSlotsAsync_ReturnsEmptyArray_WhenNoSlotsAreStored()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);

        var allSlots = await store.GetAllSlotsAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.HasCount(0, allSlots);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ReturnsSlotsFromSinglePeer()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var slots = new[]
        {
            new BusySlot("evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
            new BusySlot("evt-2", DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(3))
        };

        await store.SetSlotsAsync("peer-a", slots);
        var allSlots = await store.GetAllSlotsAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.HasCount(2, allSlots);
        Assert.Contains(s => s.SourceEventId == "evt-1", allSlots);
        Assert.Contains(s => s.SourceEventId == "evt-2", allSlots);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ReturnsSlotsFromMultiplePeers()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);

        await store.SetSlotsAsync("peer-a", [
            new BusySlot("a-evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
            new BusySlot("a-evt-2", DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(3))
        ]);

        await store.SetSlotsAsync("peer-b",
            [new BusySlot("b-evt-1", DateTimeOffset.UtcNow.AddHours(4), DateTimeOffset.UtcNow.AddHours(5))]);

        var allSlots = await store.GetAllSlotsAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.HasCount(3, allSlots);
        Assert.Contains(s => s.SourceEventId == "a-evt-1", allSlots);
        Assert.Contains(s => s.SourceEventId == "a-evt-2", allSlots);
        Assert.Contains(s => s.SourceEventId == "b-evt-1", allSlots);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ReturnsSlotsAfterReplacingPeerSlots()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);

        var initialSlots = new[]
        {
            new BusySlot("evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))
        };
        await store.SetSlotsAsync("peer-a", initialSlots);

        var replacementSlots = new[]
        {
            new BusySlot("evt-2", DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(3)),
            new BusySlot("evt-3", DateTimeOffset.UtcNow.AddHours(4), DateTimeOffset.UtcNow.AddHours(5))
        };
        await store.SetSlotsAsync("peer-a", replacementSlots);

        var allSlots = await store.GetAllSlotsAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.HasCount(2, allSlots);
        Assert.Contains(s => s.SourceEventId == "evt-2", allSlots);
        Assert.Contains(s => s.SourceEventId == "evt-3", allSlots);
        Assert.DoesNotContain(s => s.SourceEventId == "evt-1", allSlots);
    }
}
