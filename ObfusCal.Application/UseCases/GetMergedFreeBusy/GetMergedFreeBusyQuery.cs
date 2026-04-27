using MediatR;

namespace ObfusCal.Application.UseCases.GetMergedFreeBusy;

public record GetMergedFreeBusyQuery(
    Guid CalendarOwnerId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<IReadOnlyList<MergedFreeBusyResponse>>;

