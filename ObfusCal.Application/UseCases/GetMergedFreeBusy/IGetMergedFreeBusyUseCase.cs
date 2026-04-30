namespace ObfusCal.Application.UseCases.GetMergedFreeBusy;

public interface IGetMergedFreeBusyUseCase
{
    Task<IReadOnlyList<MergedFreeBusyResponse>> ExecuteAsync(GetMergedFreeBusyQuery query, CancellationToken cancellationToken);
}

