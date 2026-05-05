using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Configuration;

[TestClass]
public class DefaultLogRedactorTests
{
    [TestMethod]
    public void Redact_MasksBearerTokenAndApiKey()
    {
        var redactor = new DefaultLogRedactor();

        var input = "Authorization: Bearer eyJ.token.value apiKey=my-secret-key";
        var output = redactor.Redact(input);

        Assert.Contains("Bearer [REDACTED]", output);
        Assert.Contains("apiKey=[REDACTED]", output);
        Assert.IsFalse(output.Contains("my-secret-key", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_MasksConnectionStringPassword()
    {
        var redactor = new DefaultLogRedactor();

        var input = "Host=db;Database=obfuscal;Username=postgres;Password=postgres";
        var output = redactor.Redact(input);

        Assert.Contains("Password=[REDACTED]", output);
        Assert.IsFalse(output.Contains("Password=postgres", StringComparison.Ordinal));
    }
}

