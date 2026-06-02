namespace ObfusCal.Application.Configuration;

public sealed class PeerTransportSecurityOptions
{
    public const string SectionName = "PeerTransportSecurity";

    public bool AllowSelfSignedCerts { get; init; }

    /// <summary>
    /// Allows peer base URLs that resolve to private, loopback, or link-local addresses.
    /// Only enable this in development or demo environments where peers run on the same
    /// internal network (e.g. Docker Compose service names). Never enable in production.
    /// </summary>
    public bool AllowPrivateNetworkHosts { get; init; }
}

