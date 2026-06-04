using Microsoft.AspNetCore.Http;
using ObfusCal.Api;

namespace ObfusCal.Tests.Unit.Security;

[TestClass]
public class SecurityHeadersMiddlewareTests
{
    // --- AFTER protection: middleware sets expected headers ---

    [TestMethod]
    public async Task InvokeAsync_SetsXContentTypeOptionsHeader()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.AreEqual("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    [TestMethod]
    public async Task InvokeAsync_SetsXFrameOptionsHeader()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.AreEqual("DENY", context.Response.Headers["X-Frame-Options"].ToString());
    }

    [TestMethod]
    public async Task InvokeAsync_SetsReferrerPolicyHeader()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.AreEqual("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
    }

    [TestMethod]
    public async Task InvokeAsync_StillInvokesNextMiddleware()
    {
        var nextWasCalled = false;
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextWasCalled, "The middleware must call the next delegate in the pipeline.");
    }

    // --- BEFORE protection: demonstrates what responses look like without the middleware ---

    [TestMethod]
    public void WithoutSecurityHeadersMiddleware_XContentTypeOptionsIsAbsent()
    {
        // Simulates the pre-protection state: no middleware in the pipeline means
        // the browser will apply its own content-type sniffing, enabling MIME confusion attacks.
        var context = new DefaultHttpContext();

        Assert.IsTrue(
            string.IsNullOrEmpty(context.Response.Headers["X-Content-Type-Options"].ToString()),
            "Without SecurityHeadersMiddleware, X-Content-Type-Options is not set.");
    }

    [TestMethod]
    public void WithoutSecurityHeadersMiddleware_XFrameOptionsIsAbsent()
    {
        // Simulates the pre-protection state: without this header, the app can be embedded in
        // an iframe on any origin, enabling clickjacking attacks.
        var context = new DefaultHttpContext();

        Assert.IsTrue(
            string.IsNullOrEmpty(context.Response.Headers["X-Frame-Options"].ToString()),
            "Without SecurityHeadersMiddleware, X-Frame-Options is not set.");
    }

    [TestMethod]
    public void WithoutSecurityHeadersMiddleware_ReferrerPolicyIsAbsent()
    {
        // Simulates the pre-protection state: without an explicit policy, browsers use their
        // default behaviour and may leak the full Referer URL to third parties.
        var context = new DefaultHttpContext();

        Assert.IsTrue(
            string.IsNullOrEmpty(context.Response.Headers["Referrer-Policy"].ToString()),
            "Without SecurityHeadersMiddleware, Referrer-Policy is not set.");
    }
}

