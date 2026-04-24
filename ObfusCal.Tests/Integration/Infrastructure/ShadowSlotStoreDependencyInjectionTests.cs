using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Storage;

namespace ObfusCal.Tests.Integration.Infrastructure;

[TestClass]
public class ShadowSlotStoreDependencyInjectionTests
{
    [TestMethod]
    public void ShadowSlotStore_IsRegisteredAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Serilog.Log.Logger);
        services.AddSingleton<IShadowSlotStore, InMemoryShadowSlotStore>();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IShadowSlotStore>();
        var second = provider.GetRequiredService<IShadowSlotStore>();

        Assert.AreSame(first, second);
        Assert.IsInstanceOfType<InMemoryShadowSlotStore>(first);
    }
}
