using Microsoft.Extensions.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

public sealed class EnvironmentSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public string? GetSecret(string key)
    {
        var singleUnderscore = key.Replace(':', '_');
        var doubleUnderscore = key.Replace(":", "__", StringComparison.Ordinal);

        var envValue = Environment.GetEnvironmentVariable(doubleUnderscore)
            ?? Environment.GetEnvironmentVariable(singleUnderscore);

        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue;

        return configuration[key];
    }
}


