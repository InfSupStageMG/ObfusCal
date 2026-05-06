using System.Security.Cryptography;
using System.Text;

namespace ObfusCal.Infrastructure.Security;

public static class PeerApiKeySecurity
{
    public static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public static string ComputeSha256(string apiKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(hashBytes);
    }
}

