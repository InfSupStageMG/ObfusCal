namespace ObfusCal.Application.UseCases.GetBusySlots;

public record GetBusySlotsQuery(
    Guid CalendarOwnerId,
    DateTimeOffset From,
    DateTimeOffset To);

