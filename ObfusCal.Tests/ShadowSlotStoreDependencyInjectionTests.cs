using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Core.Interfaces;
using ObfusCal.Infrastructure.Storage;

namespace ObfusCal.Tests;

[TestClass]
public class ShadowSlotStoreDependencyInjectionTests
{
    [TestMethod]
    public void ShadowSlotStore_IsRegisteredAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IShadowSlotStore, InMemoryShadowSlotStore>();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IShadowSlotStore>();
        var second = provider.GetRequiredService<IShadowSlotStore>();

        Assert.AreSame(first, second);
        Assert.IsInstanceOfType<InMemoryShadowSlotStore>(first);
    }
}
