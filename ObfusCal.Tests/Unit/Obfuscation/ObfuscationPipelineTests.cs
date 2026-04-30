using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation;

[TestClass]
public class ObfuscationPipelineTests
{
    private const string DefaultConsultantId = "consultant-1";

    private static CalendarEvent MakeSensitiveEvent(string id = "evt-1") => new(
        Id: id,
        Title: "Confidential: Q3 Strategy Review",
        Description: "Board-level discussion â€” do not share.",
        Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
        End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
        AttendeeEmails:["alice@example.com", "bob@client.com"],
        Location: "Boardroom 3, Client HQ"
    );

    private static ObfuscationPipeline BuildPipeline(IEnumerable<IObfuscationTransformer>? transformers = null, IEnumerable<IBusySlotTransformer>? slotTransformers = null) =>
        new(transformers ?? [], slotTransformers ?? [], NullLogger<ObfuscationPipeline>.Instance);

    private static ObfuscationPipeline BuildPipeline(params IObfuscationTransformer[] transformers) =>
        new(transformers, [], NullLogger<ObfuscationPipeline>.Instance);

    private static IReadOnlyList<BusySlot> Process(ObfuscationPipeline pipeline, IEnumerable<CalendarEvent> events) =>
        pipeline.Process(events, DefaultConsultantId, ObfuscationAuditContext.Internal);


    [TestMethod]
    public void Process_WithNullConsultantId_ThrowsArgumentException()
    {
        var pipeline = BuildPipeline();
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            pipeline.Process([], null!, ObfuscationAuditContext.Internal));
    }

    [TestMethod]
    public void Process_WithEmptyConsultantId_ThrowsArgumentException()
    {
        var pipeline = BuildPipeline();
        Assert.ThrowsExactly<ArgumentException>(() =>
            pipeline.Process([], "", ObfuscationAuditContext.Internal));
    }

    [TestMethod]
    public void Process_WithWhitespaceConsultantId_ThrowsArgumentException()
    {
        var pipeline = BuildPipeline();
        Assert.ThrowsExactly<ArgumentException>(() =>
            pipeline.Process([], "   ", ObfuscationAuditContext.Internal));
    }


    [TestMethod]
    public void Process_WithNullProfile_UsesDefaultProfile()
    {
        // Default profile has RemoveTitle=true, so if transformer is present, title should be removed
        var pipeline = BuildPipeline(new RemoveTitleTransformer());
        var evt = MakeSensitiveEvent();

        var slots = pipeline.Process([evt], DefaultConsultantId, ObfuscationAuditContext.Client, null);

        Assert.AreEqual(string.Empty, slots[0].Title, "Default profile should enable title removal");
    }

    [TestMethod]
    public void Process_WithExplicitProfile_UsesProvidedProfile()
    {
        var pipeline = BuildPipeline(new RemoveTitleTransformer());
        var evt = MakeSensitiveEvent();

        var profile = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: false, // disable title removal
            RemoveDescription: true,
            RemoveLocation: true,
            RemoveAttendees: true,
            RoundTimes: false,
            RoundingIntervalMinutes: 15,
            MergeBlocks: false);

        var slots = pipeline.Process([evt], DefaultConsultantId, ObfuscationAuditContext.Client, profile);

        Assert.AreEqual(evt.Title, slots[0].Title, "Explicit profile with RemoveTitle=false should keep title");
    }


    [TestMethod]
    public void Process_WithIEnumerableEvents_StillWorks()
    {
        var pipeline = BuildPipeline();
        // Pass as a lazy IEnumerable (not a list/array)
        IEnumerable<CalendarEvent> lazyEvents = Enumerable.Range(0, 3)
            .Select(i => new CalendarEvent($"evt-{i}", "Meeting", null,
                new DateTimeOffset(2026, 6, 1, 9 + i, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10 + i, 0, 0, TimeSpan.Zero), [], null));

        var slots = Process(pipeline, lazyEvents);

        Assert.HasCount(3, slots);
    }


    [TestMethod]
    public void Process_WithProfileDisablingTitle_KeepsTitle()
    {
        var pipeline = new ObfuscationPipeline(
            [new RemoveTitleTransformer(), new RemoveDescriptionTransformer()],
            [],
            NullLogger<ObfuscationPipeline>.Instance);
        var evt = MakeSensitiveEvent();

        var profile = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: false,
            RemoveDescription: true,
            RemoveLocation: true,
            RemoveAttendees: true,
            RoundTimes: false,
            RoundingIntervalMinutes: 15,
            MergeBlocks: false);

        var slots = pipeline.Process([evt], DefaultConsultantId, ObfuscationAuditContext.Client, profile);

        Assert.AreEqual(evt.Title, slots[0].Title, "Title should be preserved when RemoveTitle=false");
        Assert.IsNull(slots[0].Description, "Description should still be removed");
    }

    [TestMethod]
    public void Process_WithProfileDisablingDescription_KeepsDescription()
    {
        var pipeline = new ObfuscationPipeline(
            [new RemoveDescriptionTransformer()],
            [],
            NullLogger<ObfuscationPipeline>.Instance);
        var evt = MakeSensitiveEvent();

        var profile = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: true,
            RemoveDescription: false,
            RemoveLocation: true,
            RemoveAttendees: true,
            RoundTimes: false,
            RoundingIntervalMinutes: 15,
            MergeBlocks: false);

        var slots = pipeline.Process([evt], DefaultConsultantId, ObfuscationAuditContext.Client, profile);

        Assert.AreEqual(evt.Description, slots[0].Description, "Description should be preserved when RemoveDescription=false");
    }

    [TestMethod]
    public void Process_WithProfileDisablingLocation_KeepsLocation()
    {
        var pipeline = new ObfuscationPipeline(
            [new RemoveLocationTransformer()],
            [],
            NullLogger<ObfuscationPipeline>.Instance);
        var evt = MakeSensitiveEvent();

        var profile = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: true,
            RemoveDescription: true,
            RemoveLocation: false,
            RemoveAttendees: true,
            RoundTimes: false,
            RoundingIntervalMinutes: 15,
            MergeBlocks: false);

        var slots = pipeline.Process([evt], DefaultConsultantId, ObfuscationAuditContext.Client, profile);

        Assert.AreEqual(evt.Location, slots[0].Location, "Location should be preserved when RemoveLocation=false");
    }

    [TestMethod]
    public void Process_WithProfileDisablingAttendees_KeepsAttendees()
    {
        var pipeline = new ObfuscationPipeline(
            [new RemoveAttendeesTransformer()],
            [],
            NullLogger<ObfuscationPipeline>.Instance);
        var evt = MakeSensitiveEvent();

        var profile = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: true,
            RemoveDescription: true,
            RemoveLocation: true,
            RemoveAttendees: false,
            RoundTimes: false,
            RoundingIntervalMinutes: 15,
            MergeBlocks: false);

        var slots = pipeline.Process([evt], DefaultConsultantId, ObfuscationAuditContext.Client, profile);

        Assert.IsNotNull(slots[0].AttendeeEmails);
        Assert.AreEqual(2, slots[0].AttendeeEmails!.Count, "Attendees should be preserved when RemoveAttendees=false");
    }

    [TestMethod]
    public void Process_WithCustomRoundingInterval_UsesProfileInterval()
    {
        var pipeline = new ObfuscationPipeline(
            [new RoundTimesTransformer()],
            [],
            NullLogger<ObfuscationPipeline>.Instance);
        var evt = new CalendarEvent("evt-1", "Meeting", null,
            new DateTimeOffset(2026, 6, 1, 9, 10, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 9, 50, 0, TimeSpan.Zero), [], null);

        var profile = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: true,
            RemoveDescription: true,
            RemoveLocation: true,
            RemoveAttendees: true,
            RoundTimes: true,
            RoundingIntervalMinutes: 30, // 30-minute rounding
            MergeBlocks: false);

        var slots = pipeline.Process([evt], DefaultConsultantId, ObfuscationAuditContext.Client, profile);

        // 9:10 rounded down to 30-min boundary = 9:00
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        // 9:50 rounded up to 30-min boundary = 10:00
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), slots[0].End);
    }

    // Pipeline Integration Tests

    [TestMethod]
    public void Process_WithNone_StillProducesBusySlotsWithCorrectWindow()
    {
        var pipeline = BuildPipeline();
        var evt = MakeSensitiveEvent();

        var slots = Process(pipeline, [evt]);

        Assert.AreEqual(evt.Start, slots[0].Start);
        Assert.AreEqual(evt.End, slots[0].End);
    }

    [TestMethod]
    public void Process_WithFullPipeline_ReturnsBusySlotsWithCorrectTimeWindow()
    {
        var pipeline = BuildPipeline(
            new RemoveTitleTransformer(),
            new RemoveDescriptionTransformer(),
            new RemoveLocationTransformer(),
            new RemoveAttendeesTransformer()
        );
        var evt = MakeSensitiveEvent();

        var slots = Process(pipeline, [evt]);

        Assert.HasCount(1, slots);
        Assert.AreEqual(evt.Start, slots[0].Start);
        Assert.AreEqual(evt.End, slots[0].End);
    }

    [TestMethod]
    public void Process_WithFullPipeline_PreservesSourceEventId()
    {
        var pipeline = BuildPipeline(
            new RemoveTitleTransformer(),
            new RemoveDescriptionTransformer(),
            new RemoveLocationTransformer(),
            new RemoveAttendeesTransformer()
        );
        var evt = MakeSensitiveEvent("unique-id-42");

        var slots = Process(pipeline, [evt]);

        Assert.AreEqual("unique-id-42", slots[0].SourceEventId);
    }

    [TestMethod]
    public void Process_WithFullPipeline_OutputContainsNoSensitiveFields()
    {
        var pipeline = BuildPipeline(
            new RemoveTitleTransformer(),
            new RemoveDescriptionTransformer(),
            new RemoveLocationTransformer(),
            new RemoveAttendeesTransformer()
        );

        var slots = Process(pipeline, [MakeSensitiveEvent()]);

        Assert.AreEqual(string.Empty, slots[0].Title);
        Assert.IsNull(slots[0].Description);
        Assert.IsNotNull(slots[0].AttendeeEmails);
        Assert.AreEqual(0, slots[0].AttendeeEmails?.Count ?? 0);
        Assert.IsNull(slots[0].Location);
    }

    [TestMethod]
    public void Process_WithEmptyEventList_ReturnsEmptyList()
    {
        var pipeline = BuildPipeline(
            new RemoveTitleTransformer(),
            new RemoveDescriptionTransformer(),
            new RemoveLocationTransformer(),
            new RemoveAttendeesTransformer()
        );

        var slots = Process(pipeline, []);

        Assert.IsEmpty(slots);
    }

    [TestMethod]
    public void Process_WithMultipleEvents_ReturnsOneSlotPerEvent()
    {
        var pipeline = BuildPipeline(
            new RemoveTitleTransformer(),
            new RemoveDescriptionTransformer(),
            new RemoveLocationTransformer(),
            new RemoveAttendeesTransformer()
        );
        // Create 5 non-overlapping events with gaps (each 1 hour apart, with 30-min gaps)
        var events = Enumerable.Range(0, 5)
            .Select(i => new CalendarEvent(
                Id: $"evt-{i}",
                Title: "Meeting",
                Description: null,
                Start: new DateTimeOffset(2026, 6, 1, 9 + (i * 2), 0, 0, TimeSpan.Zero),
                End: new DateTimeOffset(2026, 6, 1, 10 + (i * 2), 0, 0, TimeSpan.Zero),
                AttendeeEmails:[],
                Location: null
            ))
            .ToList();

        var slots = Process(pipeline, events);

        Assert.HasCount(5, slots);
    }

    [TestMethod]
    public void Process_TransformersAppliedInRegistrationOrder()
    {
        var pipeline = BuildPipeline(
            new RemoveTitleTransformer(),
            new RemoveAttendeesTransformer()
        );

        var evt = MakeSensitiveEvent();

        var afterTitle = new RemoveTitleTransformer().Transform(evt);
        var afterBoth = new RemoveAttendeesTransformer().Transform(afterTitle);

        var slots = Process(pipeline, [evt]);

        Assert.AreEqual(afterBoth.Start, slots[0].Start);
        Assert.AreEqual(afterBoth.End, slots[0].End);
    }

    [TestMethod]
    public void Process_MergesOverlappingSlots()
    {
        var pipeline = new ObfuscationPipeline([], [new MergeBlocksTransformer()], NullLogger<ObfuscationPipeline>.Instance);
        var events = new[]
        {
            new CalendarEvent("evt-1", "Meeting 1", null,
                new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-2", "Meeting 2", null,
                new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero),[], null)
        };

        var slots = Process(pipeline, events);

        Assert.HasCount(1, slots);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero), slots[0].End);
    }

    [TestMethod]
    public void Process_MergesAdjacentSlots()
    {
        var pipeline = new ObfuscationPipeline([], [new MergeBlocksTransformer()], NullLogger<ObfuscationPipeline>.Instance);
        var events = new[]
        {
            new CalendarEvent("evt-1", "Meeting 1", null,
                new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-2", "Meeting 2", null,
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero),[], null)
        };

        var slots = Process(pipeline, events);

        Assert.HasCount(1, slots);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero), slots[0].End);
    }

    [TestMethod]
    public void Process_KeepsSeparateSlots()
    {
        var pipeline = new ObfuscationPipeline([], [new MergeBlocksTransformer()], NullLogger<ObfuscationPipeline>.Instance);
        var events = new[]
        {
            new CalendarEvent("evt-1", "Meeting 1", null,
                new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-2", "Meeting 2", null,
                new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),[], null)
        };

        var slots = Process(pipeline, events);

        Assert.HasCount(2, slots);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero), slots[1].Start);
    }[TestMethod]
    public void Process_MergesMultipleOverlappingSlots()
    {
        var pipeline = new ObfuscationPipeline([], [new MergeBlocksTransformer()], NullLogger<ObfuscationPipeline>.Instance);
        var events = new[]
        {
            new CalendarEvent("evt-1", "Meeting 1", null,
                new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-2", "Meeting 2", null,
                new DateTimeOffset(2026, 6, 1, 9, 15, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-3", "Meeting 3", null,
                new DateTimeOffset(2026, 6, 1, 9, 45, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero),[], null)
        };

        var slots = Process(pipeline, events);

        Assert.HasCount(1, slots);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero), slots[0].End);
    }

    [TestMethod]
    public void Process_MergesWithRoundingTransformer()
    {
        var pipeline = new ObfuscationPipeline(
            [new RoundTimesTransformer()],
            [new MergeBlocksTransformer()],
            NullLogger<ObfuscationPipeline>.Instance
        );
        var events = new[]
        {
            new CalendarEvent("evt-1", "Meeting 1", null,
                new DateTimeOffset(2026, 6, 1, 9, 7, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 9, 37, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-2", "Meeting 2", null,
                new DateTimeOffset(2026, 6, 1, 9, 45, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 22, 0, TimeSpan.Zero),[], null)
        };

        var slots = Process(pipeline, events);

        // After rounding: slot1 is 9:00-9:45, slot2 is 9:45-10:30
        // These should merge into one: 9:00-10:30
        Assert.HasCount(1, slots);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero), slots[0].End);
    }

    [TestMethod]
    public void Process_WithProfileDisablingRoundTimes_KeepsOriginalTimes()
    {
        var pipeline = new ObfuscationPipeline(
            [new RoundTimesTransformer()],
            [],
            NullLogger<ObfuscationPipeline>.Instance);
        var evt = new CalendarEvent(
            "evt-1",
            "Meeting",
            null,
            new DateTimeOffset(2026, 6, 1, 9, 7, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 9, 37, 0, TimeSpan.Zero),
            [],
            null);

        var profile = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: true,
            RemoveDescription: true,
            RemoveLocation: true,
            RemoveAttendees: true,
            RoundTimes: false,
            RoundingIntervalMinutes: 15,
            MergeBlocks: true);

        var slots = pipeline.Process([evt], DefaultConsultantId, ObfuscationAuditContext.Client, profile);

        Assert.AreEqual(evt.Start, slots[0].Start);
        Assert.AreEqual(evt.End, slots[0].End);
    }

    [TestMethod]
    public void Process_WithProfileDisablingMergeBlocks_KeepsSeparateSlots()
    {
        var pipeline = new ObfuscationPipeline(
            [new RoundTimesTransformer()],
            [new MergeBlocksTransformer()],
            NullLogger<ObfuscationPipeline>.Instance);
        var events = new[]
        {
            new CalendarEvent("evt-1", "Meeting 1", null,
                new DateTimeOffset(2026, 6, 1, 9, 7, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 9, 37, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-2", "Meeting 2", null,
                new DateTimeOffset(2026, 6, 1, 9, 45, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 22, 0, TimeSpan.Zero),[], null)
        };

        var profile = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: true,
            RemoveDescription: true,
            RemoveLocation: true,
            RemoveAttendees: true,
            RoundTimes: true,
            RoundingIntervalMinutes: 15,
            MergeBlocks: false);

        var slots = pipeline.Process(events, DefaultConsultantId, ObfuscationAuditContext.Client, profile);

        Assert.HasCount(2, slots);
    }
}
