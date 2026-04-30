using ObfusCal.Domain.Models;
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
    public async Task GetAllSlotsAsync_IncludesSlotStartingBeforeFromButOverlapping()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        // Slot ends inside the window but starts before `from` ï¿½ overlaps, so should be included
        await store.SetSlotsAsync("peer-a", [
            new BusySlot("outside-start", from.AddMinutes(-1), from.AddHours(1))
        ]);

        var result = await store.GetAllSlotsAsync(from, to);

        Assert.HasCount(1, result, "Slot overlapping the window (Start < from, End > from) must be included");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_IncludesSlotEndingAfterToButOverlapping()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        // Slot starts inside the window but ends after `to` ï¿½ overlaps, so should be included
        await store.SetSlotsAsync("peer-a", [
            new BusySlot("outside-end", to.AddHours(-1), to.AddMinutes(1))
        ]);

        var result = await store.GetAllSlotsAsync(from, to);

        Assert.HasCount(1, result, "Slot overlapping the window (Start < to, End > to) must be included");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_IncludesSlotExactlyAtFromBoundary()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", [
            new BusySlot("at-from", from, from.AddHours(1))
        ]);

        var result = await store.GetAllSlotsAsync(from, to);

        Assert.HasCount(1, result, "Slot whose Start == from must be included");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_IncludesSlotExactlyAtToBoundary()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", [
            new BusySlot("at-to", to.AddHours(-1), to)
        ]);

        var result = await store.GetAllSlotsAsync(from, to);

        Assert.HasCount(1, result, "Slot whose End == to must be included");
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

    [TestMethod]
    public async Task SetSlotsAsync_WithOwnerScope_ReplacesOnlyThatOwnerScope()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var peerId = "peer-a";
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        await store.SetSlotsAsync(peerId, ownerA, [new BusySlot("old", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10))]);
        await store.SetSlotsAsync(peerId, ownerB, [new BusySlot("other", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10))]);

        await store.SetSlotsAsync(peerId, ownerA, [new BusySlot("new", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(20))]);

        var ownerASlots = await store.GetSlotsAsync(peerId, ownerA);
        var ownerBSlots = await store.GetSlotsAsync(peerId, ownerB);

        Assert.HasCount(1, ownerASlots);
        Assert.AreEqual("new", ownerASlots[0].SourceEventId);
        Assert.HasCount(1, ownerBSlots);
        Assert.AreEqual("other", ownerBSlots[0].SourceEventId);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_WithOwnerScope_ReturnsOnlyMatchingOwner()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        await store.SetSlotsAsync("peer-a", ownerA,
            [new BusySlot("a1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15))]);
        await store.SetSlotsAsync("peer-b", ownerB,
            [new BusySlot("b1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15))]);

        var slots = await store.GetAllSlotsAsync(ownerA, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.HasCount(1, slots);
        Assert.AreEqual("a1", slots[0].SourceEventId);
    }


    [TestMethod]
    public async Task SetSlotsAsync_WithNullPeerId_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SetSlotsAsync(null!, []));
    }

    [TestMethod]
    public async Task SetSlotsAsync_WithEmptyPeerId_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SetSlotsAsync("", []));
    }

    [TestMethod]
    public async Task SetSlotsAsync_WithWhitespacePeerId_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SetSlotsAsync("   ", []));
    }

    [TestMethod]
    public async Task SetSlotsAsync_WithNullSlots_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SetSlotsAsync("peer-a", null!));
    }

    [TestMethod]
    public async Task SetSlotsAsync_WithCancelledToken_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => store.SetSlotsAsync("peer-a", [], cts.Token));
    }

    [TestMethod]
    public async Task SetSlotsAsync_OwnerScoped_WithNullPeerId_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SetSlotsAsync(null!, Guid.NewGuid(), []));
    }

    [TestMethod]
    public async Task SetSlotsAsync_OwnerScoped_WithNullSlots_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SetSlotsAsync("peer-a", Guid.NewGuid(), null!));
    }

    [TestMethod]
    public async Task SetSlotsAsync_OwnerScoped_WithCancelledToken_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => store.SetSlotsAsync("peer-a", Guid.NewGuid(), [], cts.Token));
    }

    [TestMethod]
    public async Task GetSlotsAsync_WithNullPeerId_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.GetSlotsAsync(null!));
    }

    [TestMethod]
    public async Task GetSlotsAsync_WithCancelledToken_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => store.GetSlotsAsync("peer-a", cts.Token));
    }

    [TestMethod]
    public async Task GetSlotsAsync_OwnerScoped_WithNullPeerId_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.GetSlotsAsync(null!, Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetSlotsAsync_OwnerScoped_WithCancelledToken_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => store.GetSlotsAsync("peer-a", Guid.NewGuid(), cts.Token));
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_WithCancelledToken_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => store.GetAllSlotsAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cts.Token));
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_WithCancelledToken_Throws()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => store.GetAllSlotsAsync(Guid.NewGuid(), DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cts.Token));
    }

    [TestMethod]
    public async Task GetSlotsAsync_UnknownPeer_ReturnsEmptyList()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);

        var result = await store.GetSlotsAsync("unknown-peer");

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetSlotsAsync_OwnerScoped_UnknownPeerOwner_ReturnsEmptyList()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);

        var result = await store.GetSlotsAsync("unknown-peer", Guid.NewGuid());

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ExcludesSlotEntirelyBeforeWindow()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", [
            new BusySlot("before", from.AddHours(-3), from.AddHours(-1))
        ]);

        var result = await store.GetAllSlotsAsync(from, to);

        Assert.IsEmpty(result, "Slot entirely before window should be excluded");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ExcludesSlotEntirelyAfterWindow()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", [
            new BusySlot("after", to.AddHours(1), to.AddHours(3))
        ]);

        var result = await store.GetAllSlotsAsync(from, to);

        Assert.IsEmpty(result, "Slot entirely after window should be excluded");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ExcludesSlotEndingExactlyAtFrom()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        // Slot ends exactly at 'from' ï¿½ End <= from ? no overlap
        await store.SetSlotsAsync("peer-a", [
            new BusySlot("just-before", from.AddHours(-1), from)
        ]);

        var result = await store.GetAllSlotsAsync(from, to);

        // The filter is s.End > from, so a slot ending exactly at from should be excluded
        Assert.IsEmpty(result, "Slot ending exactly at 'from' should be excluded (no overlap)");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ExcludesSlotStartingExactlyAtTo()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        // Slot starts exactly at 'to' ï¿½ Start >= to ? no overlap
        await store.SetSlotsAsync("peer-a", [
            new BusySlot("just-after", to, to.AddHours(1))
        ]);

        var result = await store.GetAllSlotsAsync(from, to);

        // The filter is s.Start < to, so a slot starting exactly at to should be excluded
        Assert.IsEmpty(result, "Slot starting exactly at 'to' should be excluded (no overlap)");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_ExcludesSlotEntirelyOutsideWindow()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", ownerId, [
            new BusySlot("before", from.AddHours(-3), from.AddHours(-1)),
            new BusySlot("inside", from.AddHours(1), from.AddHours(2))
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, from, to);

        Assert.HasCount(1, result);
        Assert.AreEqual("inside", result[0].SourceEventId);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_FiltersToCorrectOwner()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var from = DateTimeOffset.MinValue;
        var to = DateTimeOffset.MaxValue;

        await store.SetSlotsAsync("peer-a", ownerA,
            [new BusySlot("a1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);
        await store.SetSlotsAsync("peer-a", ownerB,
            [new BusySlot("b1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        var resultA = await store.GetAllSlotsAsync(ownerA, from, to);
        var resultB = await store.GetAllSlotsAsync(ownerB, from, to);

        Assert.HasCount(1, resultA);
        Assert.AreEqual("a1", resultA[0].SourceEventId);
        Assert.HasCount(1, resultB);
        Assert.AreEqual("b1", resultB[0].SourceEventId);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_ExcludesSlotEndingExactlyAtFrom()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", ownerId, [
            new BusySlot("boundary", from.AddHours(-1), from) // End == from
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, from, to);

        Assert.IsEmpty(result, "Owner-scoped: Slot ending exactly at 'from' should be excluded");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_ExcludesSlotStartingExactlyAtTo()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", ownerId, [
            new BusySlot("boundary", to, to.AddHours(1)) // Start == to
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, from, to);

        Assert.IsEmpty(result, "Owner-scoped: Slot starting exactly at 'to' should be excluded");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_IncludesSlotOverlappingStart()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", ownerId, [
            new BusySlot("overlap", from.AddMinutes(-30), from.AddMinutes(30))
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, from, to);

        Assert.HasCount(1, result, "Owner-scoped: Slot overlapping window start should be included");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_IncludesSlotOverlappingEnd()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", ownerId, [
            new BusySlot("overlap-end", to.AddHours(-1), to.AddMinutes(30))
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, from, to);

        Assert.HasCount(1, result, "Owner-scoped: Slot overlapping window end should be included");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_ExcludesSlotEntirelyAfterWindow()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", ownerId, [
            new BusySlot("after", to.AddHours(1), to.AddHours(3))
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, from, to);

        Assert.IsEmpty(result, "Owner-scoped: Slot entirely after window should be excluded");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_IncludesSlotSpanningEntireWindow()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", ownerId, [
            new BusySlot("spanning", from.AddHours(-1), to.AddHours(1))
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, from, to);

        Assert.HasCount(1, result, "Owner-scoped: Slot spanning entire window should be included");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_IncludesSlotFullyInsideWindow()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var to   = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero);

        await store.SetSlotsAsync("peer-a", ownerId, [
            new BusySlot("inside", from.AddHours(2), from.AddHours(4))
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, from, to);

        Assert.HasCount(1, result, "Owner-scoped: Slot fully inside window should be included");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_AggregatesFromMultiplePeers()
    {
        var store = new InMemoryShadowSlotStore(Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();

        await store.SetSlotsAsync("peer-a", ownerId,
            [new BusySlot("a1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);
        await store.SetSlotsAsync("peer-b", ownerId,
            [new BusySlot("b1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        var result = await store.GetAllSlotsAsync(ownerId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.HasCount(2, result);
    }
}
