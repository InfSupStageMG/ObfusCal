using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

public sealed class PassthroughColumnEncryptor : IColumnEncryptor
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string ciphertext) => ciphertext;
}

