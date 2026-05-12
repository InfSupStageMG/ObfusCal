namespace ObfusCal.Application.Interfaces;

/// <summary>
/// Provides application-layer column encryption for sensitive database fields.
/// Implementations encrypt/decrypt individual column values using a configurable
/// symmetric key retrieved from <see cref="ISecretProvider"/>.
/// </summary>
public interface IColumnEncryptor
{
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}

