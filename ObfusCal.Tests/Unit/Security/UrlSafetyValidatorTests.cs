using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Security;

[TestClass]
public class UrlSafetyValidatorTests
{
    [TestMethod]
    public async Task ValidateAsync_ReturnsInvalid_ForHttpScheme()
    {
        var validator = new UrlSafetyValidator();

        var result = await validator.ValidateAsync("http://example.com/feed.ics");

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(UrlSafetyValidationError.UnsupportedScheme, result.Error);
    }

    [TestMethod]
    public async Task ValidateAsync_ReturnsInvalid_ForPrivateIpAddress()
    {
        var validator = new UrlSafetyValidator();

        var result = await validator.ValidateAsync("https://10.0.0.1/feed.ics");

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(UrlSafetyValidationError.PrivateNetworkHost, result.Error);
    }

    [TestMethod]
    public async Task ValidateAsync_ReturnsValid_ForPublicHttpsUrl()
    {
        var validator = new UrlSafetyValidator();

        var result = await validator.ValidateAsync("https://example.com/feed.ics");

        Assert.IsTrue(result.IsValid);
    }
}

