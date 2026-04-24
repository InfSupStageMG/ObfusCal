using ObfusCal.Core.Models;
using ObfusCal.Core.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation.Transformers;

[TestClass]
public class RoundTimesTransformerTests
{
    private static CalendarEvent MakeSensitiveEvent(string id = "evt-1") => new(
        Id: id,
        Title: "Confidential: Q3 Strategy Review",
        Description: "Board-level discussion — do not share.",
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

}
