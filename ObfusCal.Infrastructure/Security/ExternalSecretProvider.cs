using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

public sealed class ExternalSecretProvider(ILogger<ExternalSecretProvider> logger) : ISecretProvider
{
    public string? GetSecret(string key)
    {
        logger.LogDebug("External secret lookup requested for key {SecretKey}, but no external store is configured.", key);
        return null;
    }
}

