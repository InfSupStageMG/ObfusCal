using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Application.UseCases.GetBusySlots;
using ObfusCal.Application.UseCases.GetMergedFreeBusy;
using ObfusCal.Application.UseCases.PushShadowSlots;
using ObfusCal.Domain.Obfuscation;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetBusySlotsUseCase, GetBusySlotsUseCase>();
        services.AddScoped<IGetMergedFreeBusyUseCase, GetMergedFreeBusyUseCase>();
        services.AddScoped<IPushShadowSlotsUseCase, PushShadowSlotsUseCase>();

        // Register obfuscation transformers (Domain) — order determines pipeline execution order
        services.AddTransient<IObfuscationTransformer, RemoveTitleTransformer>();
        services.AddTransient<IObfuscationTransformer, RemoveDescriptionTransformer>();
        services.AddTransient<IObfuscationTransformer, RemoveLocationTransformer>();
        services.AddTransient<IObfuscationTransformer, RemoveAttendeesTransformer>();
        services.AddTransient<IObfuscationTransformer, RoundTimesTransformer>();
        services.AddTransient<IBusySlotTransformer, MergeBlocksTransformer>();

        services.AddTransient<ObfuscationPipeline>();

        return services;
    }
}

