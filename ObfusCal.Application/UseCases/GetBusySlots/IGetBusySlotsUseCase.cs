namespace ObfusCal.Application.UseCases.GetBusySlots;

public interface IGetBusySlotsUseCase
{
    Task<IReadOnlyList<BusySlotResponse>> ExecuteAsync(GetBusySlotsQuery query, CancellationToken cancellationToken);
}

