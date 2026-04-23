using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;
using ObfusCal.Core.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation;

[TestClass]
public class ObfuscationPipelineTests
{
    private static CalendarEvent MakeSensitiveEvent(string id = "evt-1") => new(
        Id: id,
        Title: "Confidential: Q3 Strategy Review",
        Description: "Board-level discussion — do not share.",
        Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
        End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
        AttendeeEmails:["alice@example.com", "bob@client.com"],
        Location: "Boardroom 3, Client HQ"
    );

    private static ObfuscationPipeline BuildPipeline(IEnumerable<IObfuscationTransformer>? transformers = null, IEnumerable<IBusySlotTransformer>? slotTransformers = null) =>
        new(transformers ??[], slotTransformers ??[], Serilog.Core.Logger.None);

    private static ObfuscationPipeline BuildPipeline(params IObfuscationTransformer[] transformers) =>
        new(transformers,[], Serilog.Core.Logger.None);

    // Pipeline Integration Tests

    [TestMethod]
    public void Process_WithNone_StillProducesBusySlotsWithCorrectWindow()
    {
        var pipeline = BuildPipeline();
        var evt = MakeSensitiveEvent();

        var slots = pipeline.Process([evt]);

        Assert.AreEqual(evt.Start, slots[0].Start);
        Assert.AreEqual(evt.End, slots[0].End);
    }[TestMethod]
    public void Process_WithFullPipeline_ReturnsBusySlotsWithCorrectTimeWindow()
    {
        var pipeline = BuildPipeline(
            new RemoveTitleTransformer(),
            new RemoveDescriptionTransformer(),
            new RemoveLocationTransformer(),
            new RemoveAttendeesTransformer()
        );
        var evt = MakeSensitiveEvent();

        var slots = pipeline.Process([evt]);

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

        var slots = pipeline.Process([evt]);

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

        var slots = pipeline.Process([MakeSensitiveEvent()]);

        var slotPropertyNames = slots[0].GetType().GetProperties().Select(p => p.Name).ToList();
        CollectionAssert.DoesNotContain(slotPropertyNames, "Title");
        CollectionAssert.DoesNotContain(slotPropertyNames, "Description");
        CollectionAssert.DoesNotContain(slotPropertyNames, "AttendeeEmails");
        CollectionAssert.DoesNotContain(slotPropertyNames, "Location");
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

        var slots = pipeline.Process([]);

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

        var slots = pipeline.Process(events);

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

        var slots = pipeline.Process([evt]);

        Assert.AreEqual(afterBoth.Start, slots[0].Start);
        Assert.AreEqual(afterBoth.End, slots[0].End);
    }

    [TestMethod]
    public void Process_MergesOverlappingSlots()
    {
        var pipeline = new ObfuscationPipeline([], [new MergeBlocksTransformer()], Serilog.Core.Logger.None);
        var events = new[]
        {
            new CalendarEvent("evt-1", "Meeting 1", null,
                new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-2", "Meeting 2", null,
                new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero),[], null)
        };

        var slots = pipeline.Process(events);

        Assert.HasCount(1, slots);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero), slots[0].End);
    }

    [TestMethod]
    public void Process_MergesAdjacentSlots()
    {
        var pipeline = new ObfuscationPipeline([], [new MergeBlocksTransformer()], Serilog.Core.Logger.None);
        var events = new[]
        {
            new CalendarEvent("evt-1", "Meeting 1", null,
                new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-2", "Meeting 2", null,
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero),[], null)
        };

        var slots = pipeline.Process(events);

        Assert.HasCount(1, slots);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero), slots[0].End);
    }

    [TestMethod]
    public void Process_KeepsSeparateSlots()
    {
        var pipeline = new ObfuscationPipeline([], [new MergeBlocksTransformer()], Serilog.Core.Logger.None);
        var events = new[]
        {
            new CalendarEvent("evt-1", "Meeting 1", null,
                new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),[], null),
            new CalendarEvent("evt-2", "Meeting 2", null,
                new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),[], null)
        };

        var slots = pipeline.Process(events);

        Assert.HasCount(2, slots);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 11, 0, 0, TimeSpan.Zero), slots[1].Start);
    }[TestMethod]
    public void Process_MergesMultipleOverlappingSlots()
    {
        var pipeline = new ObfuscationPipeline([], [new MergeBlocksTransformer()], Serilog.Core.Logger.None);
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

        var slots = pipeline.Process(events);

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
            Serilog.Core.Logger.None
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

        var slots = pipeline.Process(events);

        // After rounding: slot1 is 9:00-9:45, slot2 is 9:45-10:30
        // These should merge into one: 9:00-10:30
        Assert.HasCount(1, slots);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), slots[0].Start);
        Assert.AreEqual(new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero), slots[0].End);
    }
}
