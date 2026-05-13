namespace ObfusCal.Api;

/// <summary>
/// Adds security-related HTTP response headers to every response.
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        await next(context);
    }
}

