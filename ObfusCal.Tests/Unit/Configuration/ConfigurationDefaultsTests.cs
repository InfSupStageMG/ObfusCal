using ObfusCal.Application.Configuration;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Tests.Unit.Configuration;

[TestClass]
public class ConfigurationDefaultsTests
{
    [TestMethod]
    public void SyncOptions_HasExpectedDefaults()
    {
        var options = new SyncOptions();

        Assert.AreEqual(900, options.SyncIntervalSeconds);
        Assert.AreEqual(14, options.LookAheadDays);
    }

    [TestMethod]
    public void GraphConsentOptions_HasExpectedDefaults()
    {
        var options = new GraphConsentOptions();

        Assert.AreEqual("https://graph.microsoft.com/Calendars.Read offline_access", options.Scope);
    }

    [TestMethod]
    public void PeerConnection_ApiKeyHash_DefaultsToEmpty()
    {
        var peer = new PeerConnection
        {
            InstanceId = "test",
            BaseAddress = "https://example.com"
        };

        Assert.AreEqual(string.Empty, peer.ApiKeyHash);
    }
}

