using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Storage;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Storage;

[TestClass]
public class EfCoreCalendarOwnerAvailabilitySlotStoreTests
{
    [TestMethod]
    public async Task GetSlotsAsync_RestoresColorHex_ForSnapshotAndMergedSourceSlots()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();

        db.CalendarOwnerAvailabilitySlots.Add(new CalendarOwnerAvailabilitySlot
        {
            Id = Guid.NewGuid(),
            CalendarOwnerId = ownerId,
            SourceEventId = "slot-1",
            Start = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            SourceLabel = "Work",
            ColorHex = "#2563EB",
            SourceSlotsJson = """
                              [
                                {
                                  "sourceEventId": "source-1",
                                  "start": "2026-06-01T09:00:00+00:00",
                                  "end": "2026-06-01T10:00:00+00:00",
                                  "sourceLabel": "Work",
                                  "isAllDay": false,
                                  "colorHex": "#2563EB"
                                }
                              ]
                              """
        });
        await db.SaveChangesAsync();

        var store = new EfCoreCalendarOwnerAvailabilitySlotStore(db);

        var result = await store.GetSlotsAsync(
            ownerId,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero));

        Assert.HasCount(1, result);
        Assert.AreEqual("#2563EB", result[0].ColorHex);
        var sourceSlots = result[0].SourceSlots;
        Assert.IsNotNull(sourceSlots);
        Assert.HasCount(1, sourceSlots);
        Assert.AreEqual("#2563EB", sourceSlots[0].ColorHex);
    }
}

