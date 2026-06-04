using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Security;

[TestClass]
public class DefaultLogRedactorTests
{
    private readonly DefaultLogRedactor _redactor = new();

    [TestMethod]
    public void Redact_WithBearerToken_RedactsTokenValue()
    {
        const string input = "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.Signature";
        const string expected = "Authorization: Bearer [REDACTED]";

        var result = _redactor.Redact(input);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Redact_WithApiKeyParameter_RedactsValue()
    {
        const string input = "config: api_key=sk-abc123def456xyz789secret";

        var result = _redactor.Redact(input);

        Assert.IsFalse(result.Contains("sk-abc123def456xyz789secret", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("api_key=[REDACTED]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_WithClientSecret_RedactsValue()
    {
        const string input = "Setup: client-secret: my-super-secret-value";

        var result = _redactor.Redact(input);

        Assert.IsFalse(result.Contains("my-super-secret-value", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("[REDACTED]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_WithPassword_RedactsValue()
    {
        const string input = "Database password=P@ssw0rd123";

        var result = _redactor.Redact(input);

        Assert.IsFalse(result.Contains("P@ssw0rd123", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("password=[REDACTED]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_WithConnectionString_RedactsPasswordPart()
    {
        const string input = "Connection: Host=db;Database=obfuscal;Username=appuser;Password=Secret123!";

        var result = _redactor.Redact(input);

        Assert.IsFalse(result.Contains("Secret123!", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("Host=db", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("Password=[REDACTED]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_WithAccessToken_RedactsValue()
    {
        const string input = "oauth: access_token=ya29.a0AfH6SMBx1234567890abcdef";

        var result = _redactor.Redact(input);

        Assert.IsFalse(result.Contains("ya29.a0AfH6SMBx1234567890abcdef", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("access_token=[REDACTED]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_WithRefreshToken_RedactsValue()
    {
        const string input = "stored: refresh-token=1//0gXabc123XYZ_AbcD1234567890abcdefg";

        var result = _redactor.Redact(input);

        Assert.IsFalse(result.Contains("1//0gXabc123XYZ_AbcD1234567890abcdefg", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("[REDACTED]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_WithOAuthAuthorizationCode_RedactsCodeValue()
    {
        // Simulate an OAuth callback URL with authorization code
        const string input = "Callback URL: https://obfuscal.local/oauth/callback?code=4/0AX4XfWg1234567890abcdefghijk&state=xyz";

        var result = _redactor.Redact(input);

        // Code value should be redacted
        Assert.IsFalse(result.Contains("4/0AX4XfWg1234567890abcdefghijk", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("code=[REDACTED]", StringComparison.Ordinal));
        // Other parts should remain
        Assert.IsTrue(result.Contains("state=xyz", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("https://obfuscal.local", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_WithCodeParameterInQueryString_RedactsCodeValue()
    {
        const string input = "GraphConsent: /signin-oidc?code=0.AUgA1_2pL9ZVXYZ0123456789&state=rO3eVfkCx";

        var result = _redactor.Redact(input);

        Assert.IsFalse(result.Contains("0.AUgA1_2pL9ZVXYZ0123456789", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("code=[REDACTED]", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("state=rO3eVfkCx", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_WithMultipleCredentialTypes_RedactsAll()
    {
        const string input = "Setup: Bearer abc123; client-secret=xyz789; code=4/0AX4XfWg; password=P@ss";

        var result = _redactor.Redact(input);

        Assert.IsFalse(result.Contains("abc123", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("xyz789", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("4/0AX4XfWg", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("P@ss", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("[REDACTED]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_WithEmptyString_ReturnsEmpty()
    {
        var result = _redactor.Redact(string.Empty);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void Redact_WithNullString_ReturnsNull()
    {
        var result = _redactor.Redact(null!);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Redact_WithWhitespaceString_ReturnsWhitespace()
    {
        const string input = "   \t\n  ";

        var result = _redactor.Redact(input);

        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void Redact_WithNoSensitiveData_ReturnsUnchanged()
    {
        const string input = "This is a normal log message with no sensitive data";

        var result = _redactor.Redact(input);

        Assert.AreEqual(input, result);
    }
}

