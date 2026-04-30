using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Application.UseCases.GetBusySlots;
using ObfusCal.Application.UseCases.GetMergedFreeBusy;
using ObfusCal.Application.UseCases.PushShadowSlots;

namespace ObfusCal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetBusySlotsUseCase, GetBusySlotsUseCase>();
        services.AddScoped<IGetMergedFreeBusyUseCase, GetMergedFreeBusyUseCase>();
        services.AddScoped<IPushShadowSlotsUseCase, PushShadowSlotsUseCase>();

        services.RegisterDiscoveredEventTransformerPlugins();
        services.RegisterDiscoveredBusySlotTransformerPlugins();

        services.AddTransient<ObfuscationPipeline>();

        return services;
    }
}

