using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Infrastructure;

[TestClass]
public class SecurityHardeningTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Production_HealthEndpoint_ReturnsExpectedSecurityHeaders()
    {
        await using var factory = new CustomWebApplicationFactory("Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://obfuscal.local")
        });

        var response = await client.GetAsync("/health", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.AreEqual("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.AreEqual("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.IsTrue(response.Headers.Contains("Strict-Transport-Security"));
    }

    [TestMethod]
    public async Task Production_RegistersStrictCookiePolicyDefaults()
    {
        await using var factory = new CustomWebApplicationFactory("Production");

        var options = factory.Services.GetRequiredService<IOptions<CookiePolicyOptions>>().Value;

        Assert.AreEqual(HttpOnlyPolicy.Always, options.HttpOnly);
        Assert.AreEqual(CookieSecurePolicy.Always, options.Secure);
        Assert.AreEqual(SameSiteMode.Lax, options.MinimumSameSitePolicy);
    }
}



