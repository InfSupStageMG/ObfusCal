using System.Net;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Security;

[TestClass]
public class SecurityHeadersIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    // --- AFTER protection: every HTTP response carries the security headers ---

    [TestMethod]
    public async Task SecurityHeaders_ArePresent_OnHealthEndpoint()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [TestMethod]
    public async Task SecurityHeaders_ArePresent_OnUnauthorizedApiResponse()
    {
        // The middleware runs before authentication, so protective headers must be set
        // even on 401 responses that short-circuit the pipeline.
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/calendar-owners/me", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [TestMethod]
    public async Task SecurityHeaders_ArePresent_OnNotFoundResponse()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.IsTrue(
            response.Headers.TryGetValues("X-Content-Type-Options", out var xctOptions)
            && xctOptions.Contains("nosniff", StringComparer.OrdinalIgnoreCase),
            "X-Content-Type-Options: nosniff must be present.");

        Assert.IsTrue(
            response.Headers.TryGetValues("X-Frame-Options", out var xfo)
            && xfo.Contains("DENY", StringComparer.OrdinalIgnoreCase),
            "X-Frame-Options: DENY must be present.");

        Assert.IsTrue(
            response.Headers.TryGetValues("Referrer-Policy", out var rp)
            && rp.Any(v => !string.IsNullOrWhiteSpace(v)),
            "Referrer-Policy must be present.");
    }
}

