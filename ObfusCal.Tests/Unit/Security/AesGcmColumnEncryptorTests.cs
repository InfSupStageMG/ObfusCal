using System.Security.Cryptography;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Security;

[TestClass]
public class AesGcmColumnEncryptorTests
{
    private static IColumnEncryptor CreateEncryptor()
    {
        // Generate a fresh 256-bit key for each test run.
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var keyBase64 = Convert.ToBase64String(key);

        var secretProvider = new LiteralSecretProvider(SecretKeys.ColumnEncryptionKey, keyBase64);
        return new AesGcmColumnEncryptor(secretProvider);
    }

    [TestMethod]
    public void Encrypt_ProducesCiphertextDifferentFromPlaintext()
    {
        var encryptor = CreateEncryptor();
        const string plaintext = "super-secret-api-key-hash";

        var ciphertext = encryptor.Encrypt(plaintext);

        Assert.AreNotEqual(plaintext, ciphertext,
            "Encrypted value must not equal the plaintext.");
    }

    [TestMethod]
    public void Decrypt_RoundTrips_Correctly()
    {
        var encryptor = CreateEncryptor();
        const string plaintext = "super-secret-api-key-hash";

        var ciphertext = encryptor.Encrypt(plaintext);
        var roundTripped = encryptor.Decrypt(ciphertext);

        Assert.AreEqual(plaintext, roundTripped,
            "Decrypted value must match the original plaintext.");
    }

    [TestMethod]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertexts_PerCall()
    {
        // AES-GCM with a fresh random nonce each time means identical plaintext
        // must not produce identical ciphertext (prevents frequency analysis).
        var encryptor = CreateEncryptor();
        const string plaintext = "determinism-check";

        var cipher1 = encryptor.Encrypt(plaintext);
        var cipher2 = encryptor.Encrypt(plaintext);

        Assert.AreNotEqual(cipher1, cipher2,
            "Two encryptions of the same plaintext must produce different ciphertext (nonce must be random).");

        // Both must still decrypt correctly.
        Assert.AreEqual(plaintext, encryptor.Decrypt(cipher1));
        Assert.AreEqual(plaintext, encryptor.Decrypt(cipher2));
    }

    [TestMethod]
    public void Decrypt_WithTamperedTag_ThrowsCryptographicException()
    {
        var encryptor = CreateEncryptor();
        const string plaintext = "integrity-protected";

        var ciphertext = encryptor.Encrypt(plaintext);

        // Flip a byte in the base64 payload to simulate tampering.
        var bytes = Convert.FromBase64String(ciphertext);
        bytes[^1] ^= 0xFF; // corrupt last byte (part of ciphertext)
        var tampered = Convert.ToBase64String(bytes);

        // AuthenticationTagMismatchException inherits from CryptographicException.
        Assert.Throws<CryptographicException>(() => encryptor.Decrypt(tampered),
            "Decrypting a tampered value must throw CryptographicException (AEAD authentication failure).");
    }

    [TestMethod]
    public void Constructor_ThrowsInvalidOperation_WhenSecretMissing()
    {
        var secretProvider = new LiteralSecretProvider("other-key", "value");

        Assert.ThrowsExactly<InvalidOperationException>(
            () => new AesGcmColumnEncryptor(secretProvider),
            "Constructor must throw when the column encryption key secret is not present.");
    }

    [TestMethod]
    public void Constructor_ThrowsInvalidOperation_WhenKeyIsTooShort()
    {
        var shortKey = Convert.ToBase64String(new byte[16]); // 128-bit — too short for AES-256
        var secretProvider = new LiteralSecretProvider(SecretKeys.ColumnEncryptionKey, shortKey);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => new AesGcmColumnEncryptor(secretProvider),
            "Constructor must throw when the key is not 256 bits.");
    }

    [TestMethod]
    public void Encrypt_EmptyString_RoundTrips()
    {
        var encryptor = CreateEncryptor();

        var cipher = encryptor.Encrypt(string.Empty);
        var result = encryptor.Decrypt(cipher);

        Assert.AreEqual(string.Empty, result);
    }

    /// <summary>Minimal ISecretProvider that returns a fixed value for one specific key.</summary>
    private sealed class LiteralSecretProvider(string key, string value) : ISecretProvider
    {
        public string? GetSecret(string requestedKey)
            => requestedKey == key ? value : null;
    }
}




