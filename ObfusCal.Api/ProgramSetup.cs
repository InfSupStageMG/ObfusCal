using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ObfusCal.Api.Authentication;
using ObfusCal.Api.Authorization;
using ObfusCal.Api.RateLimiting;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.UseCases.Validation;
using Serilog;

namespace ObfusCal.Api;

internal static class ProgramSetup
{
    public static string SelectAuthenticationScheme(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return CookieAuthenticationDefaults.AuthenticationScheme;

        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorizationHeader))
            return JwtBearerDefaults.AuthenticationScheme;

        return string.Equals(authorizationHeader.Scheme, PeerApiKeyAuthenticationDefaults.SchemeName, StringComparison.OrdinalIgnoreCase) ? PeerApiKeyAuthenticationDefaults.SchemeName : JwtBearerDefaults.AuthenticationScheme;
    }

    public static void ConfigureAuthorizationPolicies(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy(AppAuthorizationPolicies.Sysadmin, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => AppAuthorizationPolicies.HasSysadminRole(context.User));
        });

        options.AddPolicy(PeerApiAuthorizationPolicies.PushShadowSlots, policy =>
        {
            policy.AddAuthenticationSchemes(PeerApiKeyAuthenticationDefaults.SchemeName);
            policy.RequireClaim(PeerApiKeyClaimTypes.Scope, PeerApiScopes.PushShadowSlots);
        });

        options.AddPolicy(PeerApiAuthorizationPolicies.PullBusySlots, policy =>
        {
            policy.AddAuthenticationSchemes(PeerApiKeyAuthenticationDefaults.SchemeName);
            policy.RequireClaim(PeerApiKeyClaimTypes.Scope, PeerApiScopes.PullBusySlots);
        });
    }

    public static void ConfigureSwaggerUi(Swashbuckle.AspNetCore.SwaggerUI.SwaggerUIOptions options, string? swaggerOAuthRedirectUri)
    {
        if (!string.IsNullOrWhiteSpace(swaggerOAuthRedirectUri))
            options.OAuth2RedirectUrl(swaggerOAuthRedirectUri);
    }

    public static async Task HandleExceptionAsync(HttpContext context)
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        if (exceptionFeature?.Error is RequestValidationException requestValidationException)
        {
            await WriteValidationProblemAsync(context, requestValidationException);
            return;
        }

        await WriteUnhandledProblemAsync(context, exceptionFeature);
    }

    public static async Task HandleApiRequestAsync(HttpContext context, RequestDelegate next)
    {
        var syncOptions = context.RequestServices.GetRequiredService<IOptions<SyncOptions>>().Value;
        if (await TryWritePayloadTooLargeAsync(context, syncOptions))
            return;

        await PeerRateLimiting.CapturePeerIdentityForRateLimitingAsync(context);

        if (await PeerRateLimiting.TryEnforceApiRequestRateLimitsAsync(context, syncOptions))
            return;

        await next(context);
    }

    private static async Task WriteValidationProblemAsync(HttpContext context, RequestValidationException requestValidationException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        var validationProblem = new ValidationProblemDetails(requestValidationException.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
        validationProblem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(validationProblem);
    }

    private static async Task WriteUnhandledProblemAsync(HttpContext context, IExceptionHandlerPathFeature? exceptionFeature)
    {
        var redactor = context.RequestServices.GetRequiredService<ILogRedactor>();
        var exception = exceptionFeature?.Error;
        var redactedMessage = redactor.Redact(exception?.Message ?? "Unhandled exception");

        Log.ForContext("RequestMethod", context.Request.Method)
            .ForContext("RequestPath", exceptionFeature?.Path ?? context.Request.Path.Value)
            .ForContext("TraceId", context.TraceIdentifier)
            .Error(exception, "Unhandled exception while processing HTTP request: {RedactedMessage}", redactedMessage);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Extensions =
            {
                ["traceId"] = context.TraceIdentifier
            }
        });
    }

    private static async Task<bool> TryWritePayloadTooLargeAsync(HttpContext context, SyncOptions syncOptions)
    {
        if (syncOptions.MaxRequestBodySizeBytes <= 0 ||
            context.Request.ContentLength is not { } contentLength ||
            contentLength <= syncOptions.MaxRequestBodySizeBytes)
        {
            return false;
        }

        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Request body exceeds the maximum allowed size."
        });

        return true;
    }
}

