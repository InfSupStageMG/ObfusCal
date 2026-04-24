using MediatR;

namespace ObfusCal.Application.UseCases.GetMergedFreeBusy;

public record GetMergedFreeBusyQuery(
    string CalendarOwnerId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<IReadOnlyList<MergedFreeBusyResponse>>;

