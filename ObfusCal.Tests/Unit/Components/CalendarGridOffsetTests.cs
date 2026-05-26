namespace ObfusCal.Tests.Unit.Components;

/// <summary>
/// Verifies the Monday-first fill-day offset formula used in MergedFreeBusyCalendarView.
/// Formula: ((int)firstDay.DayOfWeek + 6) % 7 → Monday = 0, Sunday = 6.
/// </summary>
[TestClass]
public class CalendarGridOffsetTests
{
    private static int MondayFirstOffset(DateTime date) => ((int)date.DayOfWeek + 6) % 7;

    [TestMethod]
    public void Offset_Monday_IsZero()
    {
        Assert.AreEqual(0, MondayFirstOffset(new DateTime(2026, 6, 1)));
    }

    [TestMethod]
    public void Offset_Tuesday_IsOne()
    {
        Assert.AreEqual(1, MondayFirstOffset(new DateTime(2026, 9, 1)));
    }

    [TestMethod]
    public void Offset_Wednesday_IsTwo()
    {
        Assert.AreEqual(2, MondayFirstOffset(new DateTime(2026, 7, 1)));
    }

    [TestMethod]
    public void Offset_Thursday_IsThree()
    {
        Assert.AreEqual(3, MondayFirstOffset(new DateTime(2026, 10, 1)));
    }

    [TestMethod]
    public void Offset_Friday_IsFour()
    {
        Assert.AreEqual(4, MondayFirstOffset(new DateTime(2026, 5, 1)));
    }

    [TestMethod]
    public void Offset_Saturday_IsFive()
    {
        Assert.AreEqual(5, MondayFirstOffset(new DateTime(2026, 8, 1)));
    }

    [TestMethod]
    public void Offset_Sunday_IsSix()
    {
        Assert.AreEqual(6, MondayFirstOffset(new DateTime(2026, 11, 1)));
    }

    [TestMethod]
    public void Grid_MondayStart_ProducesZeroFillCells()
    {
        var firstDay = new DateTime(2026, 6, 1);
        var fillCells = MondayFirstOffset(firstDay);
        Assert.AreEqual(0, fillCells, "A month starting on Monday must have no leading fill cells.");
    }

    [TestMethod]
    public void Grid_SundayStart_ProducesSixFillCells()
    {
        var firstDay = new DateTime(2026, 11, 1);
        var fillCells = MondayFirstOffset(firstDay);
        Assert.AreEqual(6, fillCells, "A month starting on Sunday must have 6 leading fill cells (Mon–Sat strip).");
    }
}

