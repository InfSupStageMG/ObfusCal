using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;
using ObfusCal.Core.Obfuscation.Transformers;

namespace ObfusCal.Tests;

[TestClass]
public class ObfuscationPipelineTests
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

    private static ObfuscationPipeline BuildPipeline(params IObfuscationTransformer[] transformers) =>
        new(transformers);

    // Pipeline Integration Tests

    [TestMethod]
    public void Process_WithNone_StillProducesBusySlotsWithCorrectWindow()
    {
        var pipeline = BuildPipeline();
        var evt = MakeSensitiveEvent();

        var slots = pipeline.Process([evt]);

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

        var slots = pipeline.Process([evt]);

        Assert.AreEqual(1, slots.Count);
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

        Assert.AreEqual(0, slots.Count);
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
        var events = Enumerable.Range(1, 5).Select(i => MakeSensitiveEvent($"evt-{i}")).ToList();

        var slots = pipeline.Process(events);

        Assert.AreEqual(5, slots.Count);
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

    // RemoveTitleTransformer Tests

    [TestMethod]
    public void RemoveTitleTransformer_ClearsTitle()
    {
        var transformer = new RemoveTitleTransformer();

        var result = transformer.Transform(MakeSensitiveEvent());

        Assert.AreEqual(string.Empty, result.Title);
    }

    [TestMethod]
    public void RemoveTitleTransformer_PreservesDescription()
    {
        var transformer = new RemoveTitleTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Description, result.Description);
    }

    [TestMethod]
    public void RemoveTitleTransformer_PreservesTimeWindow()
    {
        var transformer = new RemoveTitleTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Start, result.Start);
        Assert.AreEqual(evt.End, result.End);
    }

    [TestMethod]
    public void RemoveTitleTransformer_DoesNotModifyAttendees()
    {
        var transformer = new RemoveTitleTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        CollectionAssert.AreEqual(evt.AttendeeEmails.ToList(), result.AttendeeEmails.ToList());
    }

    [TestMethod]
    public void RemoveTitleTransformer_DoesNotModifyLocation()
    {
        var transformer = new RemoveTitleTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Location, result.Location);
    }

    // RemoveDescriptionTransformer Tests

    [TestMethod]
    public void RemoveDescriptionTransformer_ClearsDescription()
    {
        var transformer = new RemoveDescriptionTransformer();

        var result = transformer.Transform(MakeSensitiveEvent());

        Assert.IsNull(result.Description);
    }

    [TestMethod]
    public void RemoveDescriptionTransformer_PreservesTitle()
    {
        var transformer = new RemoveDescriptionTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Title, result.Title);
    }

    [TestMethod]
    public void RemoveDescriptionTransformer_PreservesTimeWindow()
    {
        var transformer = new RemoveDescriptionTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Start, result.Start);
        Assert.AreEqual(evt.End, result.End);
    }

    [TestMethod]
    public void RemoveDescriptionTransformer_DoesNotModifyAttendees()
    {
        var transformer = new RemoveDescriptionTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        CollectionAssert.AreEqual(evt.AttendeeEmails.ToList(), result.AttendeeEmails.ToList());
    }

    [TestMethod]
    public void RemoveDescriptionTransformer_DoesNotModifyLocation()
    {
        var transformer = new RemoveDescriptionTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Location, result.Location);
    }

    // RemoveLocationTransformer Tests

    [TestMethod]
    public void RemoveLocationTransformer_ClearsLocation()
    {
        var transformer = new RemoveLocationTransformer();

        var result = transformer.Transform(MakeSensitiveEvent());

        Assert.IsNull(result.Location);
    }

    [TestMethod]
    public void RemoveLocationTransformer_PreservesTitle()
    {
        var transformer = new RemoveLocationTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Title, result.Title);
    }

    [TestMethod]
    public void RemoveLocationTransformer_PreservesDescription()
    {
        var transformer = new RemoveLocationTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Description, result.Description);
    }

    [TestMethod]
    public void RemoveLocationTransformer_DoesNotModifyAttendees()
    {
        var transformer = new RemoveLocationTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        CollectionAssert.AreEqual(evt.AttendeeEmails.ToList(), result.AttendeeEmails.ToList());
    }

    [TestMethod]
    public void RemoveLocationTransformer_PreservesTimeWindow()
    {
        var transformer = new RemoveLocationTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Start, result.Start);
        Assert.AreEqual(evt.End, result.End);
    }

    // RemoveAttendeesTransformer Tests

    [TestMethod]
    public void RemoveAttendeesTransformer_ClearsAttendeeEmails()
    {
        var transformer = new RemoveAttendeesTransformer();

        var result = transformer.Transform(MakeSensitiveEvent());

        Assert.AreEqual(0, result.AttendeeEmails.Count);
    }

    [TestMethod]
    public void RemoveAttendeesTransformer_PreservesTitle()
    {
        var transformer = new RemoveAttendeesTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Title, result.Title);
    }

    [TestMethod]
    public void RemoveAttendeesTransformer_PreservesDescription()
    {
        var transformer = new RemoveAttendeesTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Description, result.Description);
    }

    [TestMethod]
    public void RemoveAttendeesTransformer_PreservesLocation()
    {
        var transformer = new RemoveAttendeesTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Location, result.Location);
    }

    [TestMethod]
    public void RemoveAttendeesTransformer_PreservesTimeWindow()
    {
        var transformer = new RemoveAttendeesTransformer();
        var evt = MakeSensitiveEvent();

        var result = transformer.Transform(evt);

        Assert.AreEqual(evt.Start, result.Start);
        Assert.AreEqual(evt.End, result.End);
    }
}
