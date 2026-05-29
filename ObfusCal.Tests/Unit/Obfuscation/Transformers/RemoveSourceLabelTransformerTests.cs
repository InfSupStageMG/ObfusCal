using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation.Transformers;

[TestClass]
public class RemoveSourceLabelTransformerTests
{
    private static CalendarEvent MakeEvent(string sourceLabel) => new(
        Id: "evt-1",
        Title: "Stand-up",
        Description: null,
        Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
        End: new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero),
        AttendeeEmails: [],
        Location: null,
        SourceLabel: sourceLabel
    );

    [TestMethod]
    public void Transform_ClearsSourceLabel()
    {
        var transformer = new RemoveSourceLabelTransformer();
        var result = transformer.Transform(MakeEvent("My Calendar"));

        Assert.IsNull(result.SourceLabel);
    }

    [TestMethod]
    public void Transform_LeavesOtherFieldsIntact()
    {
        var transformer = new RemoveSourceLabelTransformer();
        var original = MakeEvent("Work");

        var result = transformer.Transform(original);

        Assert.AreEqual(original.Id, result.Id);
        Assert.AreEqual(original.Title, result.Title);
        Assert.AreEqual(original.Start, result.Start);
        Assert.AreEqual(original.End, result.End);
    }

    [TestMethod]
    public void Pipeline_PreservesSourceLabel_ForInternalContext()
    {
        var pipeline = new ObfuscationPipeline([], [], NullLogger<ObfuscationPipeline>.Instance);

        var events = new[]
        {
            MakeEvent("Personal Calendar")
        };

        var profile = ObfuscationProfileSettings.CreateDefault(ObfuscationAuditContext.Internal);
        var slots = pipeline.Process(events, "owner-1", ObfuscationAuditContext.Internal, profile);

        Assert.HasCount(1, slots);
        Assert.AreEqual("Personal Calendar", slots[0].SourceLabel,
            "SourceLabel must be preserved in Internal context.");
    }

    [TestMethod]
    public void Pipeline_StripsSourceLabel_ForClientContext()
    {
        var transformer = new RemoveSourceLabelTransformer();
        var pipeline = new ObfuscationPipeline(
            [transformer],
            [],
            NullLogger<ObfuscationPipeline>.Instance);

        var events = new[]
        {
            MakeEvent("Personal Calendar")
        };

        var profile = ObfuscationProfileSettings.CreateDefault(ObfuscationAuditContext.Client);
        var slots = pipeline.Process(events, "owner-1", ObfuscationAuditContext.Client, profile);

        Assert.HasCount(1, slots);
        Assert.IsNull(slots[0].SourceLabel,
            "SourceLabel must be stripped in Client context.");
    }

    [TestMethod]
    public void Transformer_HasExpectedId()
    {
        var transformer = new RemoveSourceLabelTransformer();
        Assert.AreEqual("remove-source-label", transformer.Id);
    }
}
