using ObfusCal.Application.Calendars;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class CalendarColorPaletteTests
{
    [TestMethod]
    public void CreateReadableForeground_DarkAccentOnTint_ReturnsAccent()
        => Assert.AreEqual(
            "#2563EB",
            CalendarColorPalette.CreateReadableForeground("#2563EB", CalendarColorPalette.CreateBackgroundTint("#2563EB")));

    [TestMethod]
    public void CreateReadableForeground_WhiteAccentOnTint_ReturnsDarkFallback()
        => Assert.AreEqual(
            "#1F2937",
            CalendarColorPalette.CreateReadableForeground("#FFFFFF", CalendarColorPalette.CreateBackgroundTint("#FFFFFF")));
}

