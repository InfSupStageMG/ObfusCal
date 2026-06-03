using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Security;

[TestClass]
public class PeerApiKeySecurityTests
{
    // --- AFTER protection: PBKDF2-SHA256 hashing ---

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

    // --- BEFORE protection: demonstrates SHA256 weakness vs PBKDF2 ---

    [TestMethod]
    public void LegacySha256Hash_IsDeterministic_RevealingRainbowTableVulnerability()
    {
        // Before PBKDF2 hardening, keys were stored as plain hex SHA256 hashes.
        // SHA256 has no salt, so the same key always produces the same hash.
        // An attacker who obtained the database could use pre-computed rainbow tables
        // to recover the original keys without brute-forcing.
        const string apiKey = "peer-api-key-example";

        var hash1 = PeerApiKeySecurity.ComputeSha256(apiKey);
        var hash2 = PeerApiKeySecurity.ComputeSha256(apiKey);

        Assert.AreEqual(hash1, hash2,
            "Legacy SHA256 is deterministic: identical inputs always produce identical hashes, enabling rainbow-table attacks.");
    }

    [TestMethod]
    public void Pbkdf2Hash_IsNonDeterministic_EvenForSameKey()
    {
        // PBKDF2 incorporates a random salt per hash. Even for the same input key,
        // two invocations produce different stored hashes, rendering rainbow tables useless.
        const string apiKey = "peer-api-key-example";

        var hash1 = PeerApiKeySecurity.Hash(apiKey);
        var hash2 = PeerApiKeySecurity.Hash(apiKey);

        Assert.AreNotEqual(hash1, hash2,
            "PBKDF2 hashes must differ each time due to random salt, protecting against rainbow-table attacks.");

        // Both must still verify correctly.
        Assert.IsTrue(PeerApiKeySecurity.Verify(apiKey, hash1));
        Assert.IsTrue(PeerApiKeySecurity.Verify(apiKey, hash2));
    }
}

