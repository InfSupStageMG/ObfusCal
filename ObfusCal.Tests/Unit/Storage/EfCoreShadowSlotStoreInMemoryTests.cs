using ObfusCal.Infrastructure.Storage;
using ObfusCal.Tests.Helpers;
using BusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Tests.Unit.Storage;

/// <summary>
/// Fast EfCoreShadowSlotStore tests using InMemory DB provider.
/// These complement the Postgres integration tests without needing Docker.
/// </summary>
[TestClass]
public class EfCoreShadowSlotStoreInMemoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task SetSlotsAsync_WithNullPeerId_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SetSlotsAsync(null!, []));
    }

    [TestMethod]
    public async Task SetSlotsAsync_WithEmptyPeerId_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SetSlotsAsync("", []));
    }

    [TestMethod]
    public async Task SetSlotsAsync_WithNullSlots_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SetSlotsAsync("peer-a", null!));
    }

    [TestMethod]
    public async Task SetSlotsAsync_OwnerScoped_WithNullPeerId_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SetSlotsAsync(null!, Guid.NewGuid(), []));
    }

    [TestMethod]
    public async Task SetSlotsAsync_OwnerScoped_WithNullSlots_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SetSlotsAsync("peer-a", Guid.NewGuid(), null!));
    }

    [TestMethod]
    public async Task GetSlotsAsync_WithNullPeerId_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.GetSlotsAsync(null!));
    }

    [TestMethod]
    public async Task GetSlotsAsync_OwnerScoped_WithNullPeerId_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.GetSlotsAsync(null!, Guid.NewGuid()));
    }

    [TestMethod]
    public async Task SetAndGet_MapsAllFields()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        var slot = new BusySlot("evt-1", T0, T0.AddHours(1), "Title", "Desc", ["a@b.com"], "Room");
        await store.SetSlotsAsync("peer-x", [slot]);
        var result = await store.GetSlotsAsync("peer-x");

        Assert.HasCount(1, result);
        Assert.AreEqual("evt-1", result[0].SourceEventId);
        Assert.AreEqual(T0, result[0].Start);
        Assert.AreEqual(T0.AddHours(1), result[0].End);
        Assert.AreEqual("Title", result[0].Title);
        Assert.AreEqual("Desc", result[0].Description);
        Assert.IsNotNull(result[0].AttendeeEmails);
        Assert.AreEqual("a@b.com", result[0].AttendeeEmails![0]);
        Assert.AreEqual("Room", result[0].Location);
    }

    [TestMethod]
    public async Task SetSlotsAsync_ReplacesExistingSlotsForSamePeer()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        await store.SetSlotsAsync("peer-x", [new BusySlot("old", T0, T0.AddHours(1))]);
        await store.SetSlotsAsync("peer-x", [new BusySlot("new", T0, T0.AddHours(2))]);

        var result = await store.GetSlotsAsync("peer-x");
        Assert.HasCount(1, result);
        Assert.AreEqual("new", result[0].SourceEventId);
    }

    [TestMethod]
    public async Task SetSlotsAsync_OwnerScoped_ReplacesOnlyForThatOwner()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        await store.SetSlotsAsync("p", ownerA, [new BusySlot("a-old", T0, T0.AddHours(1))]);
        await store.SetSlotsAsync("p", ownerB, [new BusySlot("b-only", T0, T0.AddHours(1))]);
        await store.SetSlotsAsync("p", ownerA, [new BusySlot("a-new", T0, T0.AddHours(2))]);

        var a = await store.GetSlotsAsync("p", ownerA);
        var b = await store.GetSlotsAsync("p", ownerB);
        Assert.AreEqual("a-new", a[0].SourceEventId);
        Assert.AreEqual("b-only", b[0].SourceEventId);
    }

    [TestMethod]
    public async Task GetSlotsAsync_UnknownPeer_ReturnsEmpty()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        var result = await store.GetSlotsAsync("no-such-peer");
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_FiltersByTimeWindow()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var from = T0;
        var to = T0.AddHours(8);

        await store.SetSlotsAsync("p", [
            new BusySlot("before", T0.AddHours(-3), T0.AddHours(-1)),  // entirely before
            new BusySlot("inside", T0.AddHours(1), T0.AddHours(2)),    // inside window
            new BusySlot("after", T0.AddHours(9), T0.AddHours(10)),    // entirely after
            new BusySlot("overlap-start", T0.AddHours(-1), T0.AddHours(1)), // overlaps start
            new BusySlot("overlap-end", T0.AddHours(7), T0.AddHours(9))     // overlaps end
        ]);

        var result = await store.GetAllSlotsAsync(from, to);

        Assert.Contains(s => s.SourceEventId == "inside", result);
        Assert.Contains(s => s.SourceEventId == "overlap-start", result);
        Assert.Contains(s => s.SourceEventId == "overlap-end", result);
        Assert.DoesNotContain(s => s.SourceEventId == "before", result);
        Assert.DoesNotContain(s => s.SourceEventId == "after", result);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_FiltersByOwnerAndTimeWindow()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var from = T0;
        var to = T0.AddHours(8);

        await store.SetSlotsAsync("p", ownerId, [
            new BusySlot("mine-in", T0.AddHours(1), T0.AddHours(2)),
            new BusySlot("mine-out", T0.AddHours(10), T0.AddHours(11))
        ]);
        await store.SetSlotsAsync("p", otherId, [
            new BusySlot("other", T0.AddHours(1), T0.AddHours(2))
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, from, to);

        Assert.HasCount(1, result);
        Assert.AreEqual("mine-in", result[0].SourceEventId);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ExcludesSlotEndingExactlyAtFrom()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        await store.SetSlotsAsync("p", [new BusySlot("boundary", T0.AddHours(-1), T0)]);

        var result = await store.GetAllSlotsAsync(T0, T0.AddHours(8));
        Assert.IsEmpty(result, "Slot ending at exactly 'from' should be excluded");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_ExcludesSlotStartingExactlyAtTo()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var to = T0.AddHours(8);

        await store.SetSlotsAsync("p", [new BusySlot("boundary", to, to.AddHours(1))]);

        var result = await store.GetAllSlotsAsync(T0, to);
        Assert.IsEmpty(result, "Slot starting at exactly 'to' should be excluded");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OnlyReturnsUnscoped_NotOwnerScoped()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        await store.SetSlotsAsync("p", [new BusySlot("unscoped", T0, T0.AddHours(1))]);
        await store.SetSlotsAsync("p", Guid.NewGuid(), [new BusySlot("scoped", T0, T0.AddHours(1))]);

        var result = await store.GetAllSlotsAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.Contains(s => s.SourceEventId == "unscoped", result);
        Assert.DoesNotContain(s => s.SourceEventId == "scoped", result);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_MapsAllFieldsFromUnscoped()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        await store.SetSlotsAsync("p", [new BusySlot("e", T0, T0.AddHours(1), "T", "D", ["x@y.com"], "L")]);

        var result = await store.GetAllSlotsAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        Assert.HasCount(1, result);
        Assert.AreEqual("T", result[0].Title);
        Assert.AreEqual("D", result[0].Description);
        Assert.AreEqual("x@y.com", result[0].AttendeeEmails![0]);
        Assert.AreEqual("L", result[0].Location);
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_ExcludesSlotEndingExactlyAtFrom()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();

        await store.SetSlotsAsync("p", ownerId, [new BusySlot("boundary", T0.AddHours(-1), T0)]);

        var result = await store.GetAllSlotsAsync(ownerId, T0, T0.AddHours(8));
        Assert.IsEmpty(result, "Owner-scoped: Slot ending at exactly 'from' should be excluded");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_ExcludesSlotStartingExactlyAtTo()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();
        var to = T0.AddHours(8);

        await store.SetSlotsAsync("p", ownerId, [new BusySlot("boundary", to, to.AddHours(1))]);

        var result = await store.GetAllSlotsAsync(ownerId, T0, to);
        Assert.IsEmpty(result, "Owner-scoped: Slot starting at exactly 'to' should be excluded");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_IncludesSlotOverlappingWindow()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var ownerId = Guid.NewGuid();

        await store.SetSlotsAsync("p", ownerId, [
            new BusySlot("overlap", T0.AddMinutes(-30), T0.AddMinutes(30))
        ]);

        var result = await store.GetAllSlotsAsync(ownerId, T0, T0.AddHours(8));
        Assert.HasCount(1, result, "Owner-scoped: Overlapping slot should be included");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_IncludesSlotAtExactFromBoundary()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);

        // Start == from, End > from
        await store.SetSlotsAsync("p", [new BusySlot("at-from", T0, T0.AddHours(1))]);

        var result = await store.GetAllSlotsAsync(T0, T0.AddHours(8));
        Assert.HasCount(1, result, "Slot starting exactly at 'from' should be included");
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_IncludesSlotEndingAtToBoundary()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        var to = T0.AddHours(8);

        // End == to, Start < to
        await store.SetSlotsAsync("p", [new BusySlot("at-to", to.AddHours(-1), to)]);

        var result = await store.GetAllSlotsAsync(T0, to);
        Assert.HasCount(1, result, "Slot ending exactly at 'to' should still be included (End > from is true)");
    }
}

