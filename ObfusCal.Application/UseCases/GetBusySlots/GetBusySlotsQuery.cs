using MediatR;

namespace ObfusCal.Application.UseCases.GetBusySlots;

public record GetBusySlotsQuery(
    string CalendarOwnerId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<IReadOnlyList<BusySlotResponse>>;

