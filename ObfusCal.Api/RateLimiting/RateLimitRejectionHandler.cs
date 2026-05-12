using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ObfusCal.Api.RateLimiting;

internal static class RateLimitRejectionHandler
{
    internal static async ValueTask HandleRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting");
        var subject = RateLimitSubjectResolver.Resolve(httpContext);
        var retryAfterSeconds = GetRetryAfterSeconds(context.Lease);

        if (retryAfterSeconds > 0)
            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        logger.LogWarning(
            "Rate limit exceeded for {Subject} on {RequestMethod} {RequestPath}",
            subject,
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? string.Empty);

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests."
        }, cancellationToken);
    }

    internal static async Task RejectAsync(HttpContext context, string subject, int retryAfterSeconds,
        CancellationToken cancellationToken)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting");

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        logger.LogWarning(
            "Rate limit exceeded for {Subject} on {RequestMethod} {RequestPath}",
            subject,
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty);

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests."
        }, cancellationToken);
    }

    private static int GetRetryAfterSeconds(RateLimitLease lease)
    {
        if (!lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
            return 0;

        return (int)Math.Ceiling(retryAfter.TotalSeconds);
    }
}

