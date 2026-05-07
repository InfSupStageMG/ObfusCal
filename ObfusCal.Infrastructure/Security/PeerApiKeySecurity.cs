using System.Security.Cryptography;
using System.Text;

namespace ObfusCal.Infrastructure.Security;

public static class PeerApiKeySecurity
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int IterationCount = 210_000;
    private const string Pbkdf2Prefix = "PBKDF2$SHA256";

    public static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public static string Hash(string apiKey)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(apiKey),
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            HashSizeBytes);

        return $"{Pbkdf2Prefix}${IterationCount}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string apiKey, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        if (!TryParsePbkdf2(storedHash, out var iterations, out var salt, out var expectedHash))
        {
            // Compatibility path for pre-hardening hashes.
            return string.Equals(ComputeSha256(apiKey), storedHash, StringComparison.Ordinal);
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(apiKey),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public static string ComputeSha256(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes);
    }

    private static bool TryParsePbkdf2(string storedHash, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        var parts = storedHash.Split('$');
        if (parts.Length != 5)
            return false;

        if (!string.Equals(parts[0], "PBKDF2", StringComparison.Ordinal)
            || !string.Equals(parts[1], "SHA256", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[2], out iterations) || iterations <= 0)
            return false;

        try
        {
            salt = Convert.FromBase64String(parts[3]);
            hash = Convert.FromBase64String(parts[4]);
            return salt.Length > 0 && hash.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

