using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

/// <summary>
/// EF Core value converter that transparently encrypts string values at write time and
/// decrypts them at read time using the configured <see cref="IColumnEncryptor"/>.
/// Apply this converter via <c>HasConversion</c> in <see cref="AppDbContext.OnModelCreating"/>
/// to any column that must be stored encrypted at rest.
/// </summary>
internal sealed class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(IColumnEncryptor encryptor)
        : base(
            plaintext => encryptor.Encrypt(plaintext),
            stored => encryptor.Decrypt(stored))
    {
    }
}

/// <summary>
/// Nullable variant of <see cref="EncryptedStringConverter"/>.
/// </summary>
internal sealed class NullableEncryptedStringConverter : ValueConverter<string?, string?>
{
    public NullableEncryptedStringConverter(IColumnEncryptor encryptor)
        : base(
            plaintext => plaintext == null ? null : encryptor.Encrypt(plaintext),
            stored => stored == null ? null : encryptor.Decrypt(stored))
    {
    }
}

