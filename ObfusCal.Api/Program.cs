using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using ObfusCal.Api.Authentication;
using ObfusCal.Api.Authorization;
using ObfusCal.Api.Components;
using ObfusCal.Api.Controllers;
using ObfusCal.Api.RateLimiting;
using ObfusCal.Application;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.UseCases.Validation;
using ObfusCal.Infrastructure;
using ObfusCal.Infrastructure.Security;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

DotEnvLoader.Load(Path.Join(Directory.GetCurrentDirectory(), ".env"));
DotEnvLoader.Load(Path.Join(AppContext.BaseDirectory, ".env"));

try
{
    var builder = WebApplication.CreateBuilder(args);
    var azureAdSection = builder.Configuration.GetSection("AzureAd");
    var azureAdInstance = (azureAdSection["Instance"] ?? "https://login.microsoftonline.com/").TrimEnd('/');
    var azureAdTenantId = azureAdSection["TenantId"] ?? "00000000-0000-0000-0000-000000000000";
    var azureAdClientId = azureAdSection["ClientId"] ?? "00000000-0000-0000-0000-000000000000";
    var swaggerOAuthClientId = builder.Configuration["Swagger:OAuth:ClientId"] ?? azureAdClientId;
    var swaggerOAuthScope = builder.Configuration["Swagger:OAuth:Scope"] ?? $"api://{azureAdClientId}/access_as_user";
    var authorizationUrl = new Uri($"{azureAdInstance}/{azureAdTenantId}/oauth2/v2.0/authorize");
    var tokenUrl = new Uri($"{azureAdInstance}/{azureAdTenantId}/oauth2/v2.0/token");

    builder.WebHost.ConfigureKestrel((context, options) =>
    {
        var syncOptions = context.Configuration.GetSection(SyncOptions.SectionName).Get<SyncOptions>() ?? new SyncOptions();
        options.Limits.MaxRequestBodySize = syncOptions.MaxRequestBodySizeBytes;
    });

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .AddInfrastructure(builder.Configuration)
        .AddApplication();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddScheme<AuthenticationSchemeOptions, PeerApiKeyAuthenticationHandler>(
            PeerApiKeyAuthenticationDefaults.SchemeName,
            _ => { })
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

    builder.Services.AddRateLimiter(options =>
    {
        var syncOptions = builder.Configuration.GetSection(SyncOptions.SectionName).Get<SyncOptions>() ?? new SyncOptions();
        PeerRateLimiting.Configure(options, syncOptions);
    });

    builder.Services.AddHostedService(provider =>
    {
        var evictionInterval = TimeSpan.FromMinutes(1);
        return new RateLimitBucketEvictionService(evictionInterval);
    });

    builder.Services.AddAuthorization(options =>
    {
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
    });
    builder.Services.AddScoped<CalendarOwnerAccessEvaluator>();
    builder.Services.AddScoped(provider => new CalendarConsentServices(
        provider.GetRequiredService<ICalendarOwnerGraphConsentService>(),
        provider.GetRequiredService<ICalendarOwnerGoogleConsentService>()));

    builder.Services
        .AddControllers()
        .AddJsonOptions(o =>
            o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            };
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            return new BadRequestObjectResult(problem);
        };
    });

    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        options.HttpOnly = HttpOnlyPolicy.Always;
        options.Secure = CookieSecurePolicy.Always;
        options.MinimumSameSitePolicy = SameSiteMode.Lax;
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var oauthScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.OAuth2,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            BearerFormat = "JWT",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = authorizationUrl,
                    TokenUrl = tokenUrl,
                    Scopes = new Dictionary<string, string>
                    {
                        [swaggerOAuthScope] = "Access the ObfusCal API as an authenticated calendar owner"
                    }
                }
            }
        };

        options.AddSecurityDefinition("OAuth2", oauthScheme);
        options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("OAuth2")] = [swaggerOAuthScope]
        });
    });
    builder.Services.AddHealthChecks();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddFluentUIComponents();

    var app = builder.Build();

    app.Services.ValidateRequiredSecrets();

    var peerTransportSecurityOptions = app.Services.GetRequiredService<IOptions<PeerTransportSecurityOptions>>().Value;
    if (peerTransportSecurityOptions.AllowSelfSignedCerts)
    {
        Log.Warning(
            "Peer transport security is configured to allow self-signed certificates. Use this only for development or staging environments.");
    }

    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
    };
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);

    app.UseSerilogRequestLogging();
    app.UseStaticFiles();
    app.UseCookiePolicy();

    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        await next();
    });

    app.UseExceptionHandler(exceptionApp =>
    {
        exceptionApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error is RequestValidationException requestValidationException)
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
                return;
            }

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
        });
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.OAuthClientId(swaggerOAuthClientId);
            options.OAuthUsePkce();
            options.OAuthScopes(swaggerOAuthScope);
        });
    }
    else
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseAuthentication();
    app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), apiApp =>
    {
        apiApp.Use(async (context, next) =>
        {
            var syncOptions = context.RequestServices.GetRequiredService<IOptions<SyncOptions>>().Value;

            if (syncOptions.MaxRequestBodySizeBytes > 0 &&
                context.Request.ContentLength is { } contentLength &&
                contentLength > syncOptions.MaxRequestBodySizeBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status413PayloadTooLarge,
                    Title = "Request body exceeds the maximum allowed size."
                });
                return;
            }

            await PeerRateLimiting.CapturePeerIdentityForRateLimitingAsync(context);

            if (await PeerRateLimiting.TryEnforceApiRequestRateLimitsAsync(context, syncOptions))
                return;

            await next();
        });

        apiApp.UseRateLimiter();
    });
    app.UseAuthorization();
    app.UseAntiforgery();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    await app.Services.MigrateDatabaseAsync();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
