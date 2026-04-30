using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Obfuscation;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Configuration;

[TestClass]
public class ApplicationDependencyInjectionTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        // Add logger required by ObfuscationPipeline
        services.AddLogging();
        services.AddApplication();
        return services.BuildServiceProvider();
    }

    [TestMethod]
    public void AddApplication_RegistersRemoveTitleTransformer()
    {
        var provider = BuildProvider();
        var transformers = provider.GetServices<IObfuscationTransformer>().ToList();
        Assert.Contains(t => t is RemoveTitleTransformer, transformers);
    }

    [TestMethod]
    public void AddApplication_RegistersRemoveDescriptionTransformer()
    {
        var provider = BuildProvider();
        var transformers = provider.GetServices<IObfuscationTransformer>().ToList();
        Assert.Contains(t => t is RemoveDescriptionTransformer, transformers);
    }

    [TestMethod]
    public void AddApplication_RegistersRemoveLocationTransformer()
    {
        var provider = BuildProvider();
        var transformers = provider.GetServices<IObfuscationTransformer>().ToList();
        Assert.Contains(t => t is RemoveLocationTransformer, transformers);
    }

    [TestMethod]
    public void AddApplication_RegistersRemoveAttendeesTransformer()
    {
        var provider = BuildProvider();
        var transformers = provider.GetServices<IObfuscationTransformer>().ToList();
        Assert.Contains(t => t is RemoveAttendeesTransformer, transformers);
    }

    [TestMethod]
    public void AddApplication_RegistersRoundTimesTransformer()
    {
        var provider = BuildProvider();
        var transformers = provider.GetServices<IObfuscationTransformer>().ToList();
        Assert.Contains(t => t is RoundTimesTransformer, transformers);
    }

    [TestMethod]
    public void AddApplication_RegistersMergeBlocksTransformer()
    {
        var provider = BuildProvider();
        var slotTransformers = provider.GetServices<IBusySlotTransformer>().ToList();
        Assert.Contains(t => t is MergeBlocksTransformer, slotTransformers);
    }

    [TestMethod]
    public void AddApplication_RegistersObfuscationPipeline()
    {
        var provider = BuildProvider();
        var pipeline = provider.GetService<ObfuscationPipeline>();
        Assert.IsNotNull(pipeline);
    }

    [TestMethod]
    public void AddApplication_RegistersAllFiveEventTransformers()
    {
        var provider = BuildProvider();
        var transformers = provider.GetServices<IObfuscationTransformer>().ToList();
        Assert.HasCount(5, transformers, "Should register exactly 5 event transformers");
    }
}

