using System.Security.Cryptography;
using System.Text;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

/// <summary>
/// AES-256-GCM column encryptor. The encryption key is a 256-bit (32 byte) key
/// retrieved as a base64-encoded string from <see cref="ISecretProvider"/> under
/// <see cref="SecretKeys.ColumnEncryptionKey"/>.
///
/// Wire format stored in the database column (base64-encoded):
///   [ nonce (12 bytes) | tag (16 bytes) | ciphertext (n bytes) ]
///
/// A fresh random nonce is generated per call to <see cref="Encrypt"/>, so that
/// identical plaintext values produce different ciphertext on each write.
/// </summary>
internal sealed class AesGcmColumnEncryptor : IColumnEncryptor
{
    private const int NonceSize = 12; // AES-GCM standard nonce size
    private const int TagSize = 16;   // 128-bit authentication tag

    private readonly byte[] _key;

    public AesGcmColumnEncryptor(ISecretProvider secretProvider)
    {
        var keyBase64 = secretProvider.GetSecret(SecretKeys.ColumnEncryptionKey)
            ?? throw new InvalidOperationException(
                $"Secret '{SecretKeys.ColumnEncryptionKey}' is required for column-level encryption but was not found. " +
                "Generate a key with: openssl rand -base64 32");

        _key = Convert.FromBase64String(keyBase64);

        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"Secret '{SecretKeys.ColumnEncryptionKey}' must be a 256-bit (32-byte) base64-encoded key " +
                $"but had {_key.Length} bytes after decoding.");
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertextBytes, tag);

        // Pack: nonce + tag + ciphertext
        var result = new byte[NonceSize + TagSize + ciphertextBytes.Length];
        nonce.CopyTo(result.AsSpan(0));
        tag.CopyTo(result.AsSpan(NonceSize));
        ciphertextBytes.CopyTo(result.AsSpan(NonceSize + TagSize));

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedBase64)
    {
        ArgumentNullException.ThrowIfNull(encryptedBase64);

        var data = Convert.FromBase64String(encryptedBase64);

        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Encrypted column value is too short to be valid.");

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(NonceSize, TagSize);
        var ciphertextBytes = data.AsSpan(NonceSize + TagSize);

        var plaintextBytes = new byte[ciphertextBytes.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertextBytes, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}

