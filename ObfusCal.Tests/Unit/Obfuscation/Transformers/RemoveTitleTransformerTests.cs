using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation.Transformers;

[TestClass]
public class RemoveTitleTransformerTests
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

}
