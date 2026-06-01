using ObfusCal.Api.Components.Shared;
using ObfusCal.Application.Calendars;
using ObfusCal.Application.UseCases.GetMergedFreeBusy;
using ObfusCal.Domain.Models;

namespace ObfusCal.Tests.Unit.Components;

[TestClass]
public class MergedFreeBusyCalendarColorResolverTests
{
    private readonly MergedFreeBusyCalendarColorResolver _resolver = new();

    [TestMethod]
    public void BuildLabelColors_UsesSortedLabelsAndExplicitColors()
    {
        var slots = new List<MergedFreeBusyResponse>
        {
            CreateSlot(sourceLabel: "Work"),
            CreateSlot(sourceLabel: "Personal"),
            CreateSlot(sourceLabel: "Family", colorHex: "16a34a")
        };

        var labelColors = _resolver.BuildLabelColors(slots);

        Assert.AreEqual("#16A34A", labelColors["Family"].Accent);
        Assert.AreEqual(CalendarColorPalette.DefaultAccentColors[1], labelColors["Personal"].Accent);
        Assert.AreEqual(CalendarColorPalette.DefaultAccentColors[2], labelColors["Work"].Accent);
    }

    [TestMethod]
    public void ResolveForEvent_ExplicitColorOverridesLabelPalette()
    {
        var labelColors = _resolver.BuildLabelColors([CreateSlot(sourceLabel: "Work")]);
        var slot = CreateSlot(sourceLabel: "Work", colorHex: "ea580c");

        var color = _resolver.ResolveForEvent(slot, labelColors);

        Assert.AreEqual("#EA580C", color.Accent);
    }

    [TestMethod]
    public void ResolveForEvent_DifferentSourceColors_UsesMixedSourcesColor()
    {
        var labelColors = _resolver.BuildLabelColors(
        [
            CreateSlot(sourceLabel: "Work"),
            CreateSlot(sourceLabel: "Personal")
        ]);
        var mergedSlot = CreateSlot(
            sourceLabel: "Merged",
            sourceSlots:
            [
                CreateSourceSlot(sourceLabel: "Work"),
                CreateSourceSlot(sourceLabel: "Personal")
            ]);

        var color = _resolver.ResolveForEvent(mergedSlot, labelColors);

        Assert.AreEqual(_resolver.BuildMixedSourcesColor(), color);
    }

    [TestMethod]
    public void ResolveForSource_UnknownLabelWithoutColor_UsesNeutralColor()
    {
        var color = _resolver.ResolveForSource(CreateSourceSlot(sourceLabel: "Unknown"), new Dictionary<string, CalendarDisplayColor>());

        Assert.AreEqual(_resolver.BuildNeutralColor(), color);
    }

    private static MergedFreeBusyResponse CreateSlot(
        string? sourceLabel = null,
        string? colorHex = null,
        IReadOnlyList<BusySlot>? sourceSlots = null)
        => new(
            Start: new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            SourceLabel: sourceLabel,
            SourceSlots: sourceSlots,
            ColorHex: colorHex);

    private static BusySlot CreateSourceSlot(string? sourceLabel = null, string? colorHex = null)
        => new(
            SourceEventId: Guid.NewGuid().ToString("N"),
            Start: new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            SourceLabel: sourceLabel,
            ColorHex: colorHex);
}

