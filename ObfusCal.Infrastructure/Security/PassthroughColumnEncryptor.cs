using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

/// <summary>
/// No-op column encryptor that stores values as-is.
/// Used during EF Core design-time operations (migrations) and in tests that do not
/// exercise column encryption. Production code must use <see cref="AesGcmColumnEncryptor"/>.
/// </summary>
public sealed class PassthroughColumnEncryptor : IColumnEncryptor
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string ciphertext) => ciphertext;
}

