using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Obfuscation;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

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

