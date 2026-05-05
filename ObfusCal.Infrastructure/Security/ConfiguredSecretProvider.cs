using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

public sealed class ConfiguredSecretProvider(
    IOptions<SecretProviderOptions> options,
    EnvironmentSecretProvider environmentSecretProvider,
    ExternalSecretProvider externalSecretProvider) : ISecretProvider
{
    public string? GetSecret(string key)
    {
        var configuredProvider = (options.Value.Provider ?? "Environment").Trim();
        if (configuredProvider.Equals("External", StringComparison.OrdinalIgnoreCase))
            return externalSecretProvider.GetSecret(key) ?? environmentSecretProvider.GetSecret(key);

        return environmentSecretProvider.GetSecret(key);
    }
}

