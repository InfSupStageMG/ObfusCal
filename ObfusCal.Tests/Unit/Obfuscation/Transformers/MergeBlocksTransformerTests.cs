using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation.Transformers;

[TestClass]
public class MergeBlocksTransformerTests
{
    private static readonly DateTimeOffset Base = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static BusySlot Slot(string id, int startHour, int endHour) =>
        new(id, Base.AddHours(startHour), Base.AddHours(endHour));

    [TestMethod]
    public void Transform_WithEmptyList_ReturnsEmptyList()
    {
        var transformer = new MergeBlocksTransformer();

        var result = transformer.Transform([]);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Transform_WithSingleSlot_ReturnsThatSlot()
    {
        var transformer = new MergeBlocksTransformer();
        var slot = Slot("only", 9, 10);

        var result = transformer.Transform([slot]);

        Assert.HasCount(1, result);
        Assert.AreEqual(slot.Start, result[0].Start);
        Assert.AreEqual(slot.End, result[0].End);
    }

    [TestMethod]
    public void Transform_WithContainedSlot_PreservesOuterEnd()
    {
        // slot-outer (9–11) fully contains slot-inner (9:30–10:00)
        var transformer = new MergeBlocksTransformer();
        var outer = Slot("outer", 0, 2);   // 9:00–11:00
        var inner = Slot("inner", 1, 1);   // Use same absolute time hack: use minutes instead

        var outerSlot = new BusySlot("outer", Base, Base.AddHours(2));           // 9:00–11:00
        var innerSlot = new BusySlot("inner", Base.AddMinutes(30), Base.AddHours(1)); // 9:30–10:00

        var result = transformer.Transform([outerSlot, innerSlot]);

        Assert.HasCount(1, result);
        Assert.AreEqual(Base, result[0].Start,             "Merged start should be 9:00");
        Assert.AreEqual(Base.AddHours(2), result[0].End,   "Merged end must be 11:00 (outer), not 10:00 (inner)");
    }

    [TestMethod]
    public void Transform_WithNonOverlappingSlots_KeepsBoth()
    {
        var transformer = new MergeBlocksTransformer();
        var first  = Slot("first",  0, 1);   // 9:00–10:00
        var second = Slot("second", 2, 3);   // 11:00–12:00

        var result = transformer.Transform([first, second]);

        Assert.HasCount(2, result);
        Assert.AreEqual(first.Start,  result[0].Start);
        Assert.AreEqual(first.End,    result[0].End);
        Assert.AreEqual(second.Start, result[1].Start);
        Assert.AreEqual(second.End,   result[1].End);
    }

    [TestMethod]
    public void Transform_SortsUnsortedInputBeforeMerging()
    {
        // Deliberately pass in reverse order to verify sorting
        var transformer = new MergeBlocksTransformer();
        var later  = Slot("later",  2, 3);   // 11:00–12:00
        var earlier = Slot("earlier", 0, 1); // 9:00–10:00

        var result = transformer.Transform([later, earlier]);

        Assert.HasCount(2, result);
        Assert.AreEqual(earlier.Start, result[0].Start, "Earlier slot should appear first after sorting");
        Assert.AreEqual(later.Start,   result[1].Start);
    }

    [TestMethod]
    public void Transform_WithOverlapping_WhereSecondEndsEarlier_KeepsFirstEnd()
    {
        // Here, inner ends BEFORE outer, so Max must return outer.End
        var transformer = new MergeBlocksTransformer();
        var outer = new BusySlot("outer", Base, Base.AddHours(3));           // 9:00–12:00
        var inner = new BusySlot("inner", Base.AddHours(1), Base.AddHours(2)); // 10:00–11:00

        var result = transformer.Transform([outer, inner]);

        Assert.HasCount(1, result);
        Assert.AreEqual(Base.AddHours(3), result[0].End, "Max should return 12:00 (outer), not 11:00 (inner)");
    }

    [TestMethod]
    public void Transform_WithOverlapping_WhereSecondEndsLater_TakesSecondEnd()
    {
        var transformer = new MergeBlocksTransformer();
        var first = new BusySlot("first", Base, Base.AddHours(2));             // 9:00–11:00
        var second = new BusySlot("second", Base.AddHours(1), Base.AddHours(3)); // 10:00–12:00

        var result = transformer.Transform([first, second]);

        Assert.HasCount(1, result);
        Assert.AreEqual(Base.AddHours(3), result[0].End, "Max should return 12:00 (second), not 11:00 (first)");
    }

    [TestMethod]
    public void Transform_WithIdenticalEndTimes_MergesCorrectly()
    {
        var transformer = new MergeBlocksTransformer();
        var first = new BusySlot("first", Base, Base.AddHours(2));
        var second = new BusySlot("second", Base.AddHours(1), Base.AddHours(2)); // same end

        var result = transformer.Transform([first, second]);

        Assert.HasCount(1, result);
        Assert.AreEqual(Base.AddHours(2), result[0].End);
    }

    [TestMethod]
    public void Transform_WithAdjacentSlots_MergesIntoOne()
    {
        var transformer = new MergeBlocksTransformer();
        var first = new BusySlot("first", Base, Base.AddHours(1));              // 9:00–10:00
        var second = new BusySlot("second", Base.AddHours(1), Base.AddHours(2)); // 10:00–11:00

        var result = transformer.Transform([first, second]);

        Assert.HasCount(1, result);
        Assert.AreEqual(Base, result[0].Start);
        Assert.AreEqual(Base.AddHours(2), result[0].End);
    }

    [TestMethod]
    public void Transform_ThreeSlots_FirstTwoMerge_ThirdSeparate()
    {
        var transformer = new MergeBlocksTransformer();
        var a = new BusySlot("a", Base, Base.AddHours(2));             // 9:00–11:00
        var b = new BusySlot("b", Base.AddHours(1), Base.AddHours(3)); // 10:00–12:00 (overlaps a)
        var c = new BusySlot("c", Base.AddHours(5), Base.AddHours(6)); // 14:00–15:00 (separate)

        var result = transformer.Transform([a, b, c]);

        Assert.HasCount(2, result);
        Assert.AreEqual(Base, result[0].Start);
        Assert.AreEqual(Base.AddHours(3), result[0].End);
        Assert.AreEqual(Base.AddHours(5), result[1].Start);
    }
}
