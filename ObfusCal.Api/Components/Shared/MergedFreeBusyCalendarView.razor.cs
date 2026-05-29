using System.Globalization;
using Microsoft.AspNetCore.Components;
using ObfusCal.Application.UseCases.GetMergedFreeBusy;

namespace ObfusCal.Api.Components.Shared;

/// <summary>
/// Code-behind for the MergedFreeBusyCalendarView component.
/// Handles calendar grid construction, day/modal selection, timezone formatting,
/// and per-source color assignment.
/// </summary>
public partial class MergedFreeBusyCalendarView : ComponentBase
{
    [Parameter] public required List<MergedFreeBusyResponse> Slots { get; set; }
    [Parameter] public required TimeZoneInfo TimeZone { get; set; }
    [Parameter] public DateTime? DisplayDate { get; set; }

    private string _monthYear = "";
    private List<(DateTime? Date, bool HasEvents, List<MergedFreeBusyResponse> Events)> _calendarDays = [];
    private List<MergedFreeBusyResponse> _selectedDayEvents = [];
    private DateTime? _selectedDate;
    private bool _showModal;

    private DateTime _viewMonth = DateTime.UtcNow.Date;
    private List<MergedFreeBusyResponse>? _lastSeenSlots;
    private DateTime? _lastDisplayDate;
    private Dictionary<string, (string Accent, string Bg)> _labelColors = [];
    private Dictionary<DateTime, List<MergedFreeBusyResponse>> _slotsByDate = [];

    private static readonly (string Accent, string Bg)[] _palette =
    [
        ("#1976d2", "#e3f2fd"), // blue
        ("#388e3c", "#e8f5e9"), // green
        ("#7b1fa2", "#f3e5f5"), // purple
        ("#e65100", "#fff3e0"), // deep-orange
        ("#00838f", "#e0f7fa"), // cyan
        ("#ad1457", "#fce4ec"), // pink
        ("#283593", "#e8eaf6"), // indigo
        ("#558b2f", "#f9fbe7"), // light-green
    ];

    protected override void OnParametersSet()
    {
        var nextDisplayDate = DisplayDate?.Date ?? DateTime.UtcNow.Date;

        if (!ReferenceEquals(_lastSeenSlots, Slots) || _lastDisplayDate != nextDisplayDate)
        {
            _viewMonth = nextDisplayDate;
            _lastSeenSlots = Slots;
            _lastDisplayDate = nextDisplayDate;
            _selectedDate = null;
            _selectedDayEvents = [];
            _showModal = false;
            BuildLabelColors();
            BuildSlotIndex();
        }

        BuildCalendarGrid();
    }

    private void BuildLabelColors()
    {
        var labels = Slots
            .SelectMany(GetAllLabels)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        _labelColors = [];
        for (var i = 0; i < labels.Count; i++)
            _labelColors[labels[i]!] = _palette[i % _palette.Length];
    }

    private static IEnumerable<string?> GetAllLabels(MergedFreeBusyResponse slot)
    {
        yield return slot.SourceLabel;
        if (slot.SourceSlots is not { Count: > 0 }) yield break;
        foreach (var source in slot.SourceSlots)
            yield return source.SourceLabel;
    }

    private void BuildSlotIndex()
    {
        _slotsByDate = [];

        foreach (var slot in Slots)
        {
            var (startDate, endDate) = GetSlotDateRange(slot);

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (!_slotsByDate.TryGetValue(date, out var list))
                {
                    list = [];
                    _slotsByDate[date] = list;
                }

                list.Add(slot);
            }
        }

        // Sort each day's list by start time so callers get a consistent order without re-sorting
        foreach (var list in _slotsByDate.Values)
            list.Sort((a, b) => a.Start.CompareTo(b.Start));
    }

    private (DateTime StartDate, DateTime EndDate) GetSlotDateRange(MergedFreeBusyResponse slot)
    {
        if (slot.IsAllDay)
        {
            // All-day events use UTC date boundaries directly; End is exclusive per RFC 5545
            return (slot.Start.UtcDateTime.Date, slot.End.UtcDateTime.Date.AddDays(-1));
        }

        return (
            TimeZoneInfo.ConvertTime(slot.Start, TimeZone).Date,
            TimeZoneInfo.ConvertTime(slot.End, TimeZone).Date);
    }

    private (string Accent, string Bg) GetColor(string? label)
    {
        if (string.IsNullOrWhiteSpace(label) || !_labelColors.TryGetValue(label, out var color))
            return ("#9e9e9e", "#f5f5f5");
        return color;
    }

    private (string Accent, string Bg) GetSlotColor(MergedFreeBusyResponse evt)
    {
        if (evt.SourceSlots is not { Count: > 1 })
            return GetColor(evt.SourceLabel);

        var distinctLabels = evt.SourceSlots
            .Select(s => s.SourceLabel)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .ToList();

        // All sources share the same label → use that color
        return distinctLabels.Count == 1
            ? GetColor(distinctLabels[0])
            :
            // Mixed sources → neutral blue-grey to signal a multi-source merge
            ("#546e7a", "#eceff1");
    }

    private void PreviousMonth()
    {
        _viewMonth = _viewMonth.AddMonths(-1);
        BuildCalendarGrid();
    }

    private void NextMonth()
    {
        _viewMonth = _viewMonth.AddMonths(1);
        BuildCalendarGrid();
    }

    private void GoToToday()
    {
        var today = GetCurrentDateInTimeZone();
        _viewMonth = today;
        _selectedDate = today;
        _selectedDayEvents = GetEventsForDate(today);
        _showModal = false;
        BuildCalendarGrid();
    }

    private bool ShouldShowGoToTodayButton()
    {
        var today = GetCurrentDateInTimeZone();
        return _viewMonth.Year != today.Year || _viewMonth.Month != today.Month;
    }

    private DateTime GetCurrentDateInTimeZone()
        => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZone).Date;

    private void SelectDay(DateTime? date)
    {
        if (IsOutsideViewMonth(date))
            return;

        var selectedDate = date.GetValueOrDefault();
        _selectedDate = selectedDate;
        _selectedDayEvents = GetEventsForDate(selectedDate);
        _showModal = _selectedDayEvents.Count > 0;
    }

    private void CloseModal()
    {
        _showModal = false;
        _selectedDate = null;
        _selectedDayEvents = [];
    }

    private void BuildCalendarGrid()
    {
        var firstDay = new DateTime(_viewMonth.Year, _viewMonth.Month, 1);
        _monthYear = firstDay.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

        _calendarDays = [];

        // ((dow + 6) % 7) maps Monday = 0 ... Sunday = 6 (ISO 8601 week start)
        var startDayOfWeek = ((int)firstDay.DayOfWeek + 6) % 7;
        for (var i = startDayOfWeek - 1; i >= 0; i--)
            _calendarDays.Add((new DateTime(firstDay.Year, firstDay.Month, 1).AddDays(-i - 1), false, []));

        var daysInMonth = DateTime.DaysInMonth(firstDay.Year, firstDay.Month);
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(firstDay.Year, firstDay.Month, day);
            var eventsOnDay = GetEventsForDate(date);
            _calendarDays.Add((date, eventsOnDay.Count > 0, eventsOnDay));
        }

        // Fill to a fixed 6-week grid (42 cells)
        var remainingCells = 42 - _calendarDays.Count;
        for (var i = 1; i <= remainingCells; i++)
            _calendarDays.Add((new DateTime(firstDay.Year, firstDay.Month, daysInMonth).AddDays(i), false, []));
    }

    private List<MergedFreeBusyResponse> GetEventsForDate(DateTime date)
        => _slotsByDate.TryGetValue(date, out var events) ? events : [];

    private bool IsOutsideViewMonth(DateTime? date)
        => !date.HasValue || date.Value.Month != _viewMonth.Month || date.Value.Year != _viewMonth.Year;

    private string FormatTime(MergedFreeBusyResponse evt)
        => evt.IsAllDay ? "All day" : TimeZoneInfo.ConvertTime(evt.Start, TimeZone).ToString("HH:mm", CultureInfo.CurrentCulture);

    private string FormatEndTime(MergedFreeBusyResponse evt)
        => evt.IsAllDay ? "All day" : TimeZoneInfo.ConvertTime(evt.End, TimeZone).ToString("HH:mm", CultureInfo.CurrentCulture);

    private string FormatInTimeZone(DateTimeOffset value, string format)
        => TimeZoneInfo.ConvertTime(value, TimeZone).ToString(format, CultureInfo.CurrentCulture);

    private string GetDisplayDateRange()
    {
        if (Slots.Count == 0)
            return "No data";

        var minDate = Slots.Min(s => s.IsAllDay ? s.Start.UtcDateTime.Date : TimeZoneInfo.ConvertTime(s.Start, TimeZone).Date);
        var maxDate = Slots.Max(s => s.IsAllDay ? s.End.UtcDateTime.Date.AddDays(-1) : TimeZoneInfo.ConvertTime(s.End, TimeZone).Date);
        return $"{minDate:d MMM yyyy} – {maxDate:d MMM yyyy}";
    }

    private string CalculateDuration(MergedFreeBusyResponse evt)
    {
        if (evt.IsAllDay)
        {
            var days = (int)(evt.End - evt.Start).TotalDays;
            return days > 1 ? $"{days} days" : "All day";
        }

        var duration = evt.End - evt.Start;
        if (duration.TotalHours >= 1)
        {
            var hours = (int)duration.TotalHours;
            var minutes = duration.Minutes;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }

        return $"{duration.Minutes}m";
    }
}
