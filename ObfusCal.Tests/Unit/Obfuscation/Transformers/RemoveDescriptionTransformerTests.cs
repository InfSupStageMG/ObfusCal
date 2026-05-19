using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation.Transformers;

[TestClass]
public class RemoveDescriptionTransformerTests
{
    private static CalendarEvent MakeSensitiveEvent(string id = "evt-1") => new(
        Id: id,
        Title: "Confidential: Q3 Strategy Review",
        Description: "Board-level discussion - do not share.",
        Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
        End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
        AttendeeEmails: ["alice@example.com", "bob@client.com"],
        Location: "Boardroom 3, Client HQ"
    );

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
}
