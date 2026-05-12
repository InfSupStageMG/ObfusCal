namespace ObfusCal.Application.Interfaces;

/// <summary>
/// Provides application-layer column encryption for sensitive database fields.
/// Implementations encrypt/decrypt individual column values using a configurable
/// symmetric key retrieved from <see cref="ISecretProvider"/>.
/// </summary>
public interface IColumnEncryptor
{
    /// <summary>Encrypts a plaintext string value for storage in a database column.</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypts a previously encrypted column value back to its plaintext form.</summary>
    string Decrypt(string ciphertext);
}

