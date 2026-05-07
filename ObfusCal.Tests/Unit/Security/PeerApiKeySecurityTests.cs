using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Security;

[TestClass]
public class PeerApiKeySecurityTests
{
    [TestMethod]
    public void Hash_ProducesPbkdf2FormattedHash_AndVerifyPasses()
    {
        const string apiKey = "peer-key-123";

        var hash = PeerApiKeySecurity.Hash(apiKey);

        Assert.StartsWith("PBKDF2$SHA256$", hash);
        Assert.IsTrue(PeerApiKeySecurity.Verify(apiKey, hash));
    }

    [TestMethod]
    public void Verify_WithDifferentKey_Fails()
    {
        var hash = PeerApiKeySecurity.Hash("peer-key-abc");

        Assert.IsFalse(PeerApiKeySecurity.Verify("peer-key-def", hash));
    }

    [TestMethod]
    public void Verify_WithLegacySha256Hash_RemainsSupported()
    {
        const string apiKey = "legacy-key";
        var legacyHash = PeerApiKeySecurity.ComputeSha256(apiKey);

        Assert.IsTrue(PeerApiKeySecurity.Verify(apiKey, legacyHash));
        Assert.IsFalse(PeerApiKeySecurity.Verify("wrong", legacyHash));
    }
}

