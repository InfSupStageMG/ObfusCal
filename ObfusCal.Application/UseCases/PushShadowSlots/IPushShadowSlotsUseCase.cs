namespace ObfusCal.Application.UseCases.PushShadowSlots;

public interface IPushShadowSlotsUseCase
{
    Task ExecuteAsync(PushShadowSlotsCommand command, CancellationToken cancellationToken);
}

