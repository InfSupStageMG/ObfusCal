using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Security;

[TestClass]
public class PeerTransportSecurityTests
{
    [TestMethod]
    public void NormalizeThumbprint_StripsWhitespaceAndSeparators()
    {
        var normalized = PeerTransportSecurity.NormalizeThumbprint(" aa bb:cc-dd ");

        Assert.AreEqual("AABBCCDD", normalized);
    }

    [TestMethod]
    public void ValidateRemoteCertificate_AcceptsPinnedCertificate_EvenWhenChainErrorsExist()
    {
        using var certificate = CreateSelfSignedCertificate();
        var result = PeerTransportSecurity.ValidateRemoteCertificate(
            certificate,
            SslPolicyErrors.RemoteCertificateChainErrors,
            certificate.Thumbprint,
            allowSelfSignedCerts: false);

        Assert.IsTrue(result.IsTrusted);
    }

    [TestMethod]
    public void ValidateRemoteCertificate_RejectsMismatchedPinnedCertificate()
    {
        using var certificate = CreateSelfSignedCertificate();
        var result = PeerTransportSecurity.ValidateRemoteCertificate(
            certificate,
            SslPolicyErrors.None,
            "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
            allowSelfSignedCerts: true);

        Assert.IsFalse(result.IsTrusted);
        Assert.IsNotNull(result.FailureReason);
    }

    [TestMethod]
    public void ValidateRemoteCertificate_AcceptsSelfSignedCertificate_WhenExplicitlyAllowed()
    {
        using var certificate = CreateSelfSignedCertificate();
        var result = PeerTransportSecurity.ValidateRemoteCertificate(
            certificate,
            SslPolicyErrors.RemoteCertificateChainErrors,
            pinnedCertificateThumbprint: null,
            allowSelfSignedCerts: true);

        Assert.IsTrue(result.IsTrusted);
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=peer.test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}

