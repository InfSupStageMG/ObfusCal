using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class IcsCalendarEventParserTests
{
    [TestMethod]
    public void ParseEvents_SkipsEventsWithObfusCal_ManagedFlag()
    {
        var ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            UID:managed-event-uid
            SUMMARY:Placeholder
            DTSTART:20260610T080000Z
            DTEND:20260610T090000Z
            X-OBFUSCAL-MANAGED:TRUE
            X-OBFUSCAL-SLOT-ID:some-slot-id
            END:VEVENT
            END:VCALENDAR
            """;

        var events = IcsCalendarEventParser.ParseEvents(ics);

        Assert.IsEmpty(events, "Managed ObfusCal placeholders must not be included in the parsed event list.");
    }

    [TestMethod]
    public void ParseEvents_SkipsEventsWithObfusCal_ManagedFlag_CaseInsensitive()
    {
        var ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            UID:managed-lower-uid
            SUMMARY:Placeholder
            DTSTART:20260610T080000Z
            DTEND:20260610T090000Z
            X-OBFUSCAL-MANAGED:true
            END:VEVENT
            END:VCALENDAR
            """;

        var events = IcsCalendarEventParser.ParseEvents(ics);

        Assert.IsEmpty(events);
    }

    [TestMethod]
    public void ParseEvents_IncludesRegularEventsAlongside_WhenMixedWithManaged()
    {
        var ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            UID:real-event-uid
            SUMMARY:Team Meeting
            DTSTART:20260610T080000Z
            DTEND:20260610T090000Z
            END:VEVENT
            BEGIN:VEVENT
            UID:managed-event-uid
            SUMMARY:Placeholder
            DTSTART:20260610T100000Z
            DTEND:20260610T110000Z
            X-OBFUSCAL-MANAGED:TRUE
            END:VEVENT
            END:VCALENDAR
            """;

        var events = IcsCalendarEventParser.ParseEvents(ics);

        Assert.HasCount(1, events);
        Assert.AreEqual("real-event-uid", events[0].Id);
    }

    [TestMethod]
    public void ParseEvents_AssignsUniqueIds_ToRecurringEventOccurrences_WithRecurrenceId()
    {
        var ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            UID:recurring-uid
            SUMMARY:Weekly Standup
            DTSTART:20260610T090000Z
            DTEND:20260610T093000Z
            END:VEVENT
            BEGIN:VEVENT
            UID:recurring-uid
            SUMMARY:Weekly Standup (modified)
            DTSTART:20260617T090000Z
            DTEND:20260617T100000Z
            RECURRENCE-ID:20260617T090000Z
            END:VEVENT
            END:VCALENDAR
            """;

        var events = IcsCalendarEventParser.ParseEvents(ics);

        Assert.AreEqual(2, events.Count);
        var ids = events.Select(e => e.Id).ToList();
        CollectionAssert.AllItemsAreUnique(ids);
        // Master has just the UID; modified occurrence has UID:RECURRENCE-ID
        Assert.AreEqual("recurring-uid", ids[0]);
        Assert.AreEqual("recurring-uid:20260617T090000Z", ids[1]);
    }

    [TestMethod]
    public void ParseEvents_MasterOccurrence_UsesUidWithoutSuffix_WhenNoRecurrenceId()
    {
        var ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            BEGIN:VEVENT
            UID:single-uid
            SUMMARY:One-off Meeting
            DTSTART:20260610T090000Z
            DTEND:20260610T100000Z
            END:VEVENT
            END:VCALENDAR
            """;

        var events = IcsCalendarEventParser.ParseEvents(ics);

        Assert.HasCount(1, events);
        Assert.AreEqual("single-uid", events[0].Id);
    }
}

