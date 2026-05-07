using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

public sealed class SyncRuntimeOptionsProvider(
    ISecretProvider secretProvider,
    IOptions<SyncOptions> syncOptions) : ISyncRuntimeOptionsProvider
{
    public SyncOptions Get()
    {
        var configured = syncOptions.Value;

        return new SyncOptions
        {
            SyncIntervalSeconds = configured.SyncIntervalSeconds,
            LookAheadDays = configured.LookAheadDays,
            PeerRequestTimestampToleranceSeconds = configured.PeerRequestTimestampToleranceSeconds,
            KnownPeerIds = configured.KnownPeerIds,
            InstanceId = secretProvider.GetSecret(SecretKeys.SyncInstanceId) ?? configured.InstanceId,
            ApiKey = secretProvider.GetSecret(SecretKeys.SyncApiKey) ?? configured.ApiKey
        };
    }
}

