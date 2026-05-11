namespace ObfusCal.Application.Configuration;

public sealed class PeerTransportSecurityOptions
{
    public const string SectionName = "PeerTransportSecurity";

    public bool AllowSelfSignedCerts { get; init; }
}

