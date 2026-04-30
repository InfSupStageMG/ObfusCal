using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Interfaces;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Infrastructure;

[TestClass]
public class InfrastructureDependencyInjectionTests
{
    /// <summary>
    /// Uses the full-app factory (Testcontainers Postgres) to verify all infrastructure services
    /// are correctly wired and resolvable in the real container.
    /// </summary>
    private static readonly CustomWebApplicationFactory Factory = new("Development", useTestAuthentication: true);

    [TestMethod]
    public void CalendarOwnerScopeResolver_IsRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetService<ICalendarOwnerScopeResolver>();
        Assert.IsNotNull(resolver);
    }

    [TestMethod]
    public void CalendarOwnerGraphConsentService_IsRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<ICalendarOwnerGraphConsentService>();
        Assert.IsNotNull(svc);
    }

    [TestMethod]
    public void CalendarOwnerIcalFeedService_IsRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<ICalendarOwnerIcalFeedService>();
        Assert.IsNotNull(svc);
    }

    [TestMethod]
    public void CalendarOwnerObfuscationProfileService_IsRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<ICalendarOwnerObfuscationProfileService>();
        Assert.IsNotNull(svc);
    }

    [TestMethod]
    public void OutboundPeerSyncService_IsRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<IOutboundPeerSyncService>();
        Assert.IsNotNull(svc);
    }

    [TestMethod]
    public void InboundPeerPullSyncService_IsRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<IInboundPeerPullSyncService>();
        Assert.IsNotNull(svc);
    }

    [TestMethod]
    public void ShadowSlotStore_IsRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<IShadowSlotStore>();
        Assert.IsNotNull(svc);
    }

    [TestMethod]
    public void CalendarSource_IsRegistered()
    {
        using var scope = Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<ICalendarSource>();
        Assert.IsNotNull(svc);
    }
}

