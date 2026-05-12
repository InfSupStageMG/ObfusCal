using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
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
        Assert.AreEqual(300, options.PeerRequestTimestampToleranceSeconds);
        Assert.AreEqual(240, options.PeerRequestRateLimitPermitLimit);
        Assert.AreEqual(60, options.PeerRequestRateLimitWindowSeconds);
        Assert.AreEqual(60, options.PushShadowSlotsRateLimitPermitLimit);
        Assert.AreEqual(60, options.PushShadowSlotsRateLimitWindowSeconds);
        Assert.AreEqual(120, options.PullBusySlotsRateLimitPermitLimit);
        Assert.AreEqual(60, options.PullBusySlotsRateLimitWindowSeconds);
        Assert.AreEqual(1_048_576, options.MaxRequestBodySizeBytes);
        Assert.AreEqual(90, options.MaxQueryWindowDays);
        Assert.AreEqual(500, options.MaxShadowSlotsPerRequest);
    }

    [TestMethod]
    public void PeerTransportSecurityOptions_DefaultsToRejectingSelfSignedCertificates()
    {
        var options = new PeerTransportSecurityOptions();

        Assert.IsFalse(options.AllowSelfSignedCerts);
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
        Assert.AreEqual(PeerApiScopes.DefaultSerializedScopes, peer.Scopes);
        Assert.IsNull(peer.PinnedCertificateThumbprint);
        Assert.IsNull(peer.ClientCertificateThumbprint);
    }
}

