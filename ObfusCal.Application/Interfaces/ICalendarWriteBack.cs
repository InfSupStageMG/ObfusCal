using ObfusCal.Domain.Models;

namespace ObfusCal.Application.Interfaces;

/// <summary>Writes obfuscated busy-slot placeholders back into a calendar destination.</summary>
public interface ICalendarWriteBack
{
    Task WriteBackSlotsAsync(
        Guid calendarOwnerId,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default);
}

/// <summary>Writes obfuscated busy-slot placeholders back into a specific calendar source instance.</summary>
public interface ICalendarSourceInstanceWriteBack
{
    Task WriteBackSlotsAsync(
        CalendarSourceInstanceContext instance,
        IReadOnlyList<BusySlot> busySlots,
        string placeholderTitle,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct = default);
}
