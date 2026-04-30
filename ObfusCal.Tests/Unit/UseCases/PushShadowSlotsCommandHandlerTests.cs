using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.UseCases.PushShadowSlots;
using ObfusCal.Domain.Models;

namespace ObfusCal.Tests.Unit.UseCases;

[TestClass]
public class PushShadowSlotsCommandHandlerTests
{
    [TestMethod]
    public async Task Handle_StoresSlotsWithPeerIdPrefix()
    {
        var store = new CapturingShadowSlotStore();
        var handler = new PushShadowSlotsUseCase(store, NullLogger<PushShadowSlotsUseCase>.Instance);

        var ownerId = Guid.NewGuid();
        var command = new PushShadowSlotsCommand(
            "peer-x",
            [ownerId],
            [new ShadowSlotInput(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.HasCount(1, store.CapturedSlots);
        Assert.IsTrue(store.CapturedSlots[0].SourceEventId.StartsWith("peer-x-"),
            "Slot SourceEventId should start with peerId prefix");
    }

    [TestMethod]
    public async Task Handle_CreatesCorrectSourceEventId_WithIndex()
    {
        var store = new CapturingShadowSlotStore();
        var handler = new PushShadowSlotsUseCase(store, NullLogger<PushShadowSlotsUseCase>.Instance);

        var ownerId = Guid.NewGuid();
        var command = new PushShadowSlotsCommand(
            "peer-x",
            [ownerId],
            [
                new ShadowSlotInput(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
                new ShadowSlotInput(DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(3))
            ]);

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.AreEqual("peer-x-0", store.CapturedSlots[0].SourceEventId);
        Assert.AreEqual("peer-x-1", store.CapturedSlots[1].SourceEventId);
    }

    [TestMethod]
    public async Task Handle_StoresSlotsForEachDistinctOwner()
    {
        var store = new CapturingShadowSlotStore();
        var handler = new PushShadowSlotsUseCase(store, NullLogger<PushShadowSlotsUseCase>.Instance);

        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var command = new PushShadowSlotsCommand(
            "peer-x",
            [ownerA, ownerB],
            [new ShadowSlotInput(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.AreEqual(2, store.SetCallCount, "Should call SetSlotsAsync once per distinct owner");
    }

    [TestMethod]
    public async Task Handle_DeduplicatesOwnerIds()
    {
        var store = new CapturingShadowSlotStore();
        var handler = new PushShadowSlotsUseCase(store, NullLogger<PushShadowSlotsUseCase>.Instance);

        var ownerId = Guid.NewGuid();
        var command = new PushShadowSlotsCommand(
            "peer-x",
            [ownerId, ownerId, ownerId],
            [new ShadowSlotInput(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.AreEqual(1, store.SetCallCount, "Duplicate owner IDs should be deduplicated");
    }

    [TestMethod]
    public async Task Handle_MapsAllSlotFields()
    {
        var store = new CapturingShadowSlotStore();
        var handler = new PushShadowSlotsUseCase(store, NullLogger<PushShadowSlotsUseCase>.Instance);

        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(1);
        var command = new PushShadowSlotsCommand(
            "peer-x",
            [Guid.NewGuid()],
            [new ShadowSlotInput(start, end, "Title", "Desc", ["a@b.com"], "Loc")]);

        await handler.ExecuteAsync(command, CancellationToken.None);

        var slot = store.CapturedSlots[0];
        Assert.AreEqual(start, slot.Start);
        Assert.AreEqual(end, slot.End);
        Assert.AreEqual("Title", slot.Title);
        Assert.AreEqual("Desc", slot.Description);
        Assert.AreEqual("a@b.com", slot.AttendeeEmails![0]);
        Assert.AreEqual("Loc", slot.Location);
    }

    private sealed class CapturingShadowSlotStore : IShadowSlotStore
    {
        public List<BusySlot> CapturedSlots { get; } = [];
        public int SetCallCount { get; private set; }

        public Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default)
        {
            SetCallCount++;
            CapturedSlots.AddRange(slots);
            return Task.CompletedTask;
        }

        public Task SetSlotsAsync(string peerId, Guid calendarOwnerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default)
        {
            SetCallCount++;
            CapturedSlots.AddRange(slots);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BusySlot>>([]);
        public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, Guid calendarOwnerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BusySlot>>([]);
        public Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BusySlot>>([]);
        public Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(Guid calendarOwnerId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BusySlot>>([]);
    }
}

