using ObfusCal.Api.Components.CalendarOwnerDetail;
using ObfusCal.Application.Calendars;

namespace ObfusCal.Tests.Unit.Components;

[TestClass]
public class CalendarColorFieldDisplayTests
{
    [TestMethod]
    public void ResolvePreviewColor_ExplicitValue_ReturnsNormalizedColor()
        => Assert.AreEqual("#16A34A", CalendarColorFieldDisplay.ResolvePreviewColor("16a34a"));

    [TestMethod]
    public void ResolvePreviewColor_InvalidValue_UsesAutomaticColor()
        => Assert.AreEqual(
            "#16A34A",
            CalendarColorFieldDisplay.ResolvePreviewColor("not-a-color", "16a34a"));

    [TestMethod]
    public void ResolvePreviewColor_InvalidValueWithoutAutomaticFallback_FallsBackToFirstPreset()
        => Assert.AreEqual(
            CalendarColorPalette.DefaultAccentColors[0],
            CalendarColorFieldDisplay.ResolvePreviewColor("not-a-color"));

    [TestMethod]
    public void ResolveAutomaticColor_UsesSortedSourceLabelsLikeDashboard()
        => Assert.AreEqual(
            CalendarColorPalette.DefaultAccentColors[1],
            CalendarColorFieldDisplay.ResolveAutomaticColor(
                "Personal",
                ["Work", "Personal", "Family"]));

    [TestMethod]
    public void DescribeSelection_EmptyValue_ReturnsAutomaticPalette()
        => Assert.AreEqual("Automatic palette", CalendarColorFieldDisplay.DescribeSelection(null));

    [TestMethod]
    public void IsPresetSelected_NormalizesMatchingValues()
        => Assert.IsTrue(CalendarColorFieldDisplay.IsPresetSelected("2563eb", "#2563EB"));

}

