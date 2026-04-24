using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation.Transformers;

[TestClass]
public class RemoveAttendeesTransformerTests
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
    public void RemoveAttendeesTransformer_ClearsAttendeeEmails()
    {
        var transformer = new RemoveAttendeesTransformer();

        var result = transformer.Transform(MakeSensitiveEvent());

        Assert.IsEmpty(result.AttendeeEmails);
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
