using ObfusCal.Application.UseCases.GetMergedFreeBusy;

namespace ObfusCal.Tests.Unit.Components;

/// <summary>
/// Tests for all-day event date-indexing logic that is central to MergedFreeBusyCalendarView.
/// The view's BuildSlotIndex treats all-day events differently from timed events:
/// - no timezone conversion (UTC date boundaries are used as-is)
/// - End is exclusive (an event with End = May 11 00:00 UTC appears only on May 10)
/// These tests exercise the same indexing logic in isolation so they stay fast and deterministic.
/// </summary>
[TestClass]
public class AllDayEventSlotIndexTests
{
    private static readonly TimeZoneInfo Utc2 = TimeZoneInfo.CreateCustomTimeZone(
        "Test/UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");

    private static Dictionary<DateTime, List<MergedFreeBusyResponse>> BuildIndex(
        IEnumerable<MergedFreeBusyResponse> slots,
        TimeZoneInfo tz)
    {
        var index = new Dictionary<DateTime, List<MergedFreeBusyResponse>>();

        foreach (var slot in slots)
        {
            DateTime startDate, endDate;

            if (slot.IsAllDay)
            {
                startDate = slot.Start.UtcDateTime.Date;
                endDate = slot.End.UtcDateTime.Date.AddDays(-1);
            }
            else
            {
                startDate = TimeZoneInfo.ConvertTime(slot.Start, tz).Date;
                endDate = TimeZoneInfo.ConvertTime(slot.End, tz).Date;
            }

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (!index.TryGetValue(date, out var list))
                {
                    list = [];
                    index[date] = list;
                }

                list.Add(slot);
            }
        }

        return index;
    }

    [TestMethod]
    public void AllDayEvent_AppearsOnlyOnMay10_NotMay11_ForUtcPlus2User()
    {
        var slot = new MergedFreeBusyResponse(
            new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero),
            IsAllDay: true);

        var index = BuildIndex([slot], Utc2);

        Assert.IsTrue(index.ContainsKey(new DateTime(2026, 5, 10)), "All-day event must appear on May 10.");
        Assert.IsFalse(index.ContainsKey(new DateTime(2026, 5, 11)), "All-day event must NOT appear on May 11 (End is exclusive).");
    }

    [TestMethod]
    public void MultiDayAllDayEvent_AppearsOnEachDay_ExclusiveEnd()
    {
        var slot = new MergedFreeBusyResponse(
            new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero),
            IsAllDay: true);

        var index = BuildIndex([slot], Utc2);

        Assert.IsTrue(index.ContainsKey(new DateTime(2026, 5, 10)), "Must appear on May 10.");
        Assert.IsTrue(index.ContainsKey(new DateTime(2026, 5, 11)), "Must appear on May 11.");
        Assert.IsTrue(index.ContainsKey(new DateTime(2026, 5, 12)), "Must appear on May 12.");
        Assert.IsFalse(index.ContainsKey(new DateTime(2026, 5, 13)), "Must NOT appear on May 13 (exclusive end).");
    }

    [TestMethod]
    public void TimedEvent_StillUsesTimezoneConversion()
    {
        var slot = new MergedFreeBusyResponse(
            new DateTimeOffset(2026, 5, 10, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 11, 1, 0, 0, TimeSpan.Zero),
            IsAllDay: false);

        var index = BuildIndex([slot], Utc2);

        Assert.IsTrue(index.ContainsKey(new DateTime(2026, 5, 11)));
    }

    [TestMethod]
    public void AllDayEvent_IsAllDayFalse_UsesTimezoneConversion_AndProducesGhostDay()
    {
        var slot = new MergedFreeBusyResponse(
            new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero),
            IsAllDay: false);

        var index = BuildIndex([slot], Utc2);

        Assert.IsTrue(index.ContainsKey(new DateTime(2026, 5, 11)),
            "Without IsAllDay flag, timezone conversion causes the ghost day to appear. This is expected for truly cross-day timed events.");
    }
}

