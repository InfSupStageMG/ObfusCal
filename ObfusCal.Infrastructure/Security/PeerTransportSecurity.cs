using System.Net.Security;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace ObfusCal.Infrastructure.Security;

public static class PeerTransportRequestOptions
{
    public static readonly HttpRequestOptionsKey<Guid> PeerConnectionId = new("ObfusCal.PeerConnectionId");
    public static readonly HttpRequestOptionsKey<string> PeerInstanceId = new("ObfusCal.PeerInstanceId");
    public static readonly HttpRequestOptionsKey<string> PinnedCertificateThumbprint = new("ObfusCal.PinnedCertificateThumbprint");
    public static readonly HttpRequestOptionsKey<string> ClientCertificateThumbprint = new("ObfusCal.ClientCertificateThumbprint");
}

public sealed record PeerTransportCertificateValidationResult(bool IsTrusted, string? FailureReason)
{
    public static PeerTransportCertificateValidationResult Success() => new(true, null);
    public static PeerTransportCertificateValidationResult Fail(string reason) => new(false, reason);
}

public static class PeerTransportSecurity
{
    public static string? NormalizeThumbprint(string? thumbprint)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
            return null;

        var normalized = new string(thumbprint.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public static PeerTransportCertificateValidationResult ValidateRemoteCertificate(
        X509Certificate? certificate,
        SslPolicyErrors sslPolicyErrors,
        string? pinnedCertificateThumbprint,
        bool allowSelfSignedCerts)
    {
        if (certificate is null)
            return PeerTransportCertificateValidationResult.Fail("The remote peer did not present a certificate.");

        var actualThumbprint = NormalizeThumbprint(certificate.GetCertHashString());
        var expectedThumbprint = NormalizeThumbprint(pinnedCertificateThumbprint);

        if (expectedThumbprint is not null && !string.Equals(actualThumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase))
        {
            return PeerTransportCertificateValidationResult.Fail(
                $"Remote certificate thumbprint mismatch. Expected {expectedThumbprint}, got {actualThumbprint ?? "<unknown>"}.");
        }

        if (expectedThumbprint is not null)
            return PeerTransportCertificateValidationResult.Success();

        if (sslPolicyErrors == SslPolicyErrors.None)
            return PeerTransportCertificateValidationResult.Success();

        if (allowSelfSignedCerts)
            return PeerTransportCertificateValidationResult.Success();

        return PeerTransportCertificateValidationResult.Fail($"Remote certificate validation failed: {sslPolicyErrors}.");
    }

    public static X509Certificate2? TryResolveClientCertificate(
        string? thumbprint,
        ILogger logger,
        string? peerInstanceId = null)
    {
        var normalizedThumbprint = NormalizeThumbprint(thumbprint);
        if (normalizedThumbprint is null)
            return null;

        foreach (var storeLocation in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            try
            {
                using var store = new X509Store(StoreName.My, storeLocation);
                store.Open(OpenFlags.ReadOnly);

                var matches = store.Certificates.Find(X509FindType.FindByThumbprint, normalizedThumbprint, validOnly: false);
                foreach (var certificate in matches)
                {
                    if (!certificate.HasPrivateKey)
                    {
                        logger.LogWarning(
                            "Ignoring client certificate {Thumbprint} for peer {PeerId} because it does not contain a private key.",
                            normalizedThumbprint,
                            peerInstanceId ?? "<unknown>");
                        continue;
                    }

                    return certificate;
                }
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to load client certificate {Thumbprint} for peer {PeerId} from {StoreLocation} store.",
                    normalizedThumbprint,
                    peerInstanceId ?? "<unknown>",
                    storeLocation);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to load client certificate {Thumbprint} for peer {PeerId} from {StoreLocation} store.",
                    normalizedThumbprint,
                    peerInstanceId ?? "<unknown>",
                    storeLocation);
            }
            catch (SecurityException ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to load client certificate {Thumbprint} for peer {PeerId} from {StoreLocation} store.",
                    normalizedThumbprint,
                    peerInstanceId ?? "<unknown>",
                    storeLocation);
            }
        }

        logger.LogWarning(
            "No client certificate with thumbprint {Thumbprint} was found for peer {PeerId}.",
            normalizedThumbprint,
            peerInstanceId ?? "<unknown>");
        return null;
    }
}


