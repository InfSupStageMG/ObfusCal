using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation.Transformers;

[TestClass]
public class RoundTimesTransformerTests
{
    private static CalendarEvent MakeSensitiveEvent(string id = "evt-1") => new(
        Id: id,
        Title: "Confidential: Q3 Strategy Review",
        Description: "Board-level discussion ï¿½ do not share.",
        Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
        End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
        AttendeeEmails: ["alice@example.com", "bob@client.com"],
        Location: "Boardroom 3, Client HQ"
    );

    [TestMethod]
    public void RoundTimesTransformer_RoundsStartTimeDown()
    {
        var transformer = new RoundTimesTransformer();
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 9, 7, 30, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // 9:07:30 should round down to 9:00
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), result.Start);
    }

    [TestMethod]
    public void RoundTimesTransformer_RoundsEndTimeUp()
    {
        var transformer = new RoundTimesTransformer();
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 10, 7, 30, TimeSpan.Zero),
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // 10:07:30 should round up to 10:15
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 10, 15, 0, TimeSpan.Zero), result.End);
    }

    [TestMethod]
    public void RoundTimesTransformer_RoundsAlignedTimesUnchanged()
    {
        var transformer = new RoundTimesTransformer();
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Start, result.Start);
        Assert.AreEqual(evt.End, result.End);
    }

    [TestMethod]
    public void RoundTimesTransformer_RoundsStartTo15MinuteBoundary()
    {
        var transformer = new RoundTimesTransformer();
        var testCases = new[] { (1, 0), (7, 0), (14, 0), (22, 15), (29, 15) };

        foreach (var (minute, expectedRoundedMinute) in testCases)
        {
            var evt = new CalendarEvent(
                Id: "evt-1",
                Title: "Meeting",
                Description: null,
                Start: new DateTimeOffset(2026, 6, 1, 9, minute, 0, TimeSpan.Zero),
                End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
                AttendeeEmails: [],
                Location: null
            );

            var result = transformer.Transform(evt);

            Assert.AreEqual(9, result.Start.Hour);
            Assert.AreEqual(expectedRoundedMinute, result.Start.Minute, $"Minute {minute} should round down to {expectedRoundedMinute}");
        }
    }

    [TestMethod]
    public void RoundTimesTransformer_HandlesSpecialCase30Minutes()
    {
        var transformer = new RoundTimesTransformer();
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // 9:30 should stay at 9:30 as it's already aligned
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero), result.Start);
    }

    [TestMethod]
    public void RoundTimesTransformer_PreservesTitle()
    {
        var transformer = new RoundTimesTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Title, result.Title);
    }

    [TestMethod]
    public void RoundTimesTransformer_PreservesDescription()
    {
        var transformer = new RoundTimesTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Description, result.Description);
    }

    [TestMethod]
    public void RoundTimesTransformer_PreservesAttendees()
    {
        var transformer = new RoundTimesTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        CollectionAssert.AreEqual(evt.AttendeeEmails.ToList(), result.AttendeeEmails.ToList());
    }

    [TestMethod]
    public void RoundTimesTransformer_RoundsEndTimeCrossingMidnight_ToStartOfNextDay()
    {
        var transformer = new RoundTimesTransformer();
        var evt = new CalendarEvent(
            Id: "evt-midnight",
            Title: "Late Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 23, 52, 0, TimeSpan.Zero), // rounds up past midnight
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // 23:52 rounds up to 24:00 which is 00:00 on the next day
        Assert.AreEqual(new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), result.End);
    }


    [TestMethod]
    public void RoundTimesTransformer_WithZeroInterval_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RoundTimesTransformer(0));
    }

    [TestMethod]
    public void RoundTimesTransformer_WithNegativeInterval_ThrowsArgumentOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RoundTimesTransformer(-5));
    }

    [TestMethod]
    public void RoundTimesTransformer_WithPositiveInterval_DoesNotThrow()
    {
        var transformer = new RoundTimesTransformer(1);
        Assert.IsNotNull(transformer);
    }

    [TestMethod]
    public void RoundTimesTransformer_WithCustomInterval_RoundsCorrectly()
    {
        var transformer = new RoundTimesTransformer(30);
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 9, 10, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 9, 50, 0, TimeSpan.Zero),
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // 9:10 rounded down to 30-min = 9:00
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), result.Start);
        // 9:50 rounded up to 30-min = 10:00
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), result.End);
    }

    [TestMethod]
    public void RoundTimesTransformer_RoundUp_ExactlyOnBoundary_StaysSame()
    {
        // End time is exactly on a 15-min boundary: should not change
        var transformer = new RoundTimesTransformer();
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 10, 15, 0, TimeSpan.Zero),
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 10, 15, 0, TimeSpan.Zero), result.End,
            "End time already on 15-min boundary should stay unchanged");
    }

    [TestMethod]
    public void RoundTimesTransformer_RoundUp_MidnightNonZeroRemainder()
    {
        // End time crosses midnight with a non-zero remainder: e.g., 23:50 with 30-min interval
        var transformer = new RoundTimesTransformer(30);
        var evt = new CalendarEvent(
            Id: "evt-midnight",
            Title: "Late Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 23, 40, 0, TimeSpan.Zero), // rounds up to 00:00
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // 23:40 rounds up to next 30-min boundary = 24:00 = midnight next day
        Assert.AreEqual(new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), result.End);
    }

    [TestMethod]
    public void RoundTimesTransformer_RoundDown_JustPastBoundary()
    {
        var transformer = new RoundTimesTransformer(15);
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 9, 16, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // 9:16 rounds down to 9:15
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 15, 0, TimeSpan.Zero), result.Start);
    }

    [TestMethod]
    public void RoundTimesTransformer_Arithmetic_WithOneMinuteInterval()
    {
        var transformer = new RoundTimesTransformer(1);
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 9, 7, 30, TimeSpan.Zero), // has seconds
            End: new DateTimeOffset(2026, 6, 1, 10, 0, 30, TimeSpan.Zero),   // has seconds
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // With 1-min interval: 9:07:30 ? floor(547.5 / 1) * 1 = 547 min = 9:07
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 7, 0, TimeSpan.Zero), result.Start);
        // 10:00:30 ? ceil(600.5 / 1) * 1 = 601 min = 10:01
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 10, 1, 0, TimeSpan.Zero), result.End);
    }

    [TestMethod]
    public void RoundTimesTransformer_EndExactlyAtMidnight_StaysAtMidnight()
    {
        var transformer = new RoundTimesTransformer(15);
        var evt = new CalendarEvent(
            Id: "evt-midnight-exact",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), // midnight exactly = 1440 min
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // Midnight is already on boundary, Ceiling(1440/15)*15 = 1440 => hits >= branch
        Assert.AreEqual(new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), result.End);
    }

    [TestMethod]
    public void RoundTimesTransformer_EndJustBeforeMidnight_RoundsUpToMidnight()
    {
        // 23:59 with 15-min interval: Ceiling(1439/15)*15 = Ceiling(95.93)*15 = 96*15 = 1440
        var transformer = new RoundTimesTransformer(15);
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 23, 59, 0, TimeSpan.Zero),
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        Assert.AreEqual(new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), result.End);
    }

    [TestMethod]
    public void RoundTimesTransformer_EndJustPastMidnight_RoundsToNextBoundary()
    {
        // 23:46 with 15-min interval: Ceiling(1426/15)*15 = Ceiling(95.07)*15 = 96*15 = 1440 = midnight
        // This ensures the midnight branch adds 1 day
        var transformer = new RoundTimesTransformer(15);
        var evt = new CalendarEvent(
            Id: "evt-1",
            Title: "Meeting",
            Description: null,
            Start: new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 23, 46, 0, TimeSpan.Zero),
            AttendeeEmails: [],
            Location: null
        );

        var result = transformer.Transform(evt);

        // 23:46 rounds up to next 15-min boundary = 24:00 = midnight next day
        Assert.AreEqual(new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), result.End);
    }

    [TestMethod]
    public void RoundTimesTransformer_PreservesLocation()
    {
        var transformer = new RoundTimesTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Location, result.Location);
    }

    [TestMethod]
    public void RoundTimesTransformer_PreservesId()
    {
        var transformer = new RoundTimesTransformer();
        var evt = MakeSensitiveEvent("custom-id");

        var result = transformer.Transform(evt);

        Assert.AreEqual("custom-id", result.Id);
    }
}
