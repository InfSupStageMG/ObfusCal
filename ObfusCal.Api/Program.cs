using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.OpenApi;
using ObfusCal.Api;
using ObfusCal.Api.Authentication;
using ObfusCal.Api.Authorization;
using ObfusCal.Api.Components;
using ObfusCal.Api.Controllers;
using ObfusCal.Api.RateLimiting;
using ObfusCal.Application;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
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
    const string appAuthenticationScheme = "AppAuthentication";

    var builder = WebApplication.CreateBuilder(args);
    var azureAdSection = builder.Configuration.GetSection("AzureAd");
    var azureAdInstance = (azureAdSection["Instance"] ?? "https://login.microsoftonline.com/").TrimEnd('/');
    var azureAdTenantId = azureAdSection["TenantId"] ?? "00000000-0000-0000-0000-000000000000";
    var azureAdClientId = azureAdSection["ClientId"] ?? "00000000-0000-0000-0000-000000000000";
    var swaggerOAuthClientId = builder.Configuration["Swagger:OAuth:ClientId"] ?? azureAdClientId;
    var swaggerOAuthScope = builder.Configuration["Swagger:OAuth:Scope"] ?? $"api://{azureAdClientId}/access_as_user";
    var swaggerOAuthRedirectUri = builder.Configuration["Swagger:OAuth:RedirectUri"];
    var authorizationUrl = new Uri($"{azureAdInstance}/{azureAdTenantId}/oauth2/v2.0/authorize");
    var tokenUrl = new Uri($"{azureAdInstance}/{azureAdTenantId}/oauth2/v2.0/token");

    builder.WebHost.ConfigureKestrel((context, options) =>
    {
        var syncOptions = context.Configuration.GetSection(SyncOptions.SectionName).Get<SyncOptions>() ??
                          new SyncOptions();
        options.Limits.MaxRequestBodySize = syncOptions.MaxRequestBodySizeBytes;
    });

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .AddInfrastructure(builder.Configuration)
        .AddApplication();

    builder.Services.AddHttpContextAccessor();

    var authenticationBuilder = builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = appAuthenticationScheme;
            options.DefaultAuthenticateScheme = appAuthenticationScheme;
            options.DefaultChallengeScheme = appAuthenticationScheme;
        })
        .AddPolicyScheme(appAuthenticationScheme, "ObfusCal application authentication",
            options => { options.ForwardDefaultSelector = ProgramSetup.SelectAuthenticationScheme; })
        .AddScheme<AuthenticationSchemeOptions, PeerApiKeyAuthenticationHandler>(
            PeerApiKeyAuthenticationDefaults.SchemeName,
            _ => { });

    authenticationBuilder.AddMicrosoftIdentityWebApi(
        builder.Configuration.GetSection("AzureAd"),
        jwtBearerScheme: JwtBearerDefaults.AuthenticationScheme);

    authenticationBuilder.AddMicrosoftIdentityWebApp(
        builder.Configuration.GetSection("AzureAd"),
        openIdConnectScheme: OpenIdConnectDefaults.AuthenticationScheme,
        cookieScheme: CookieAuthenticationDefaults.AuthenticationScheme);

    builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme,
        options =>
        {
            options.LoginPath = "/account/login";
            options.LogoutPath = "/account/logout";
            options.AccessDeniedPath = "/account/access-denied";
            options.SlidingExpiration = true;
        });

    builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.UsePkce = true;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = false;
    });

    builder.Services.AddRateLimiter(options =>
    {
        var syncOptions = builder.Configuration.GetSection(SyncOptions.SectionName).Get<SyncOptions>() ??
                          new SyncOptions();
        PeerRateLimiting.Configure(options, syncOptions);
    });

    builder.Services.AddHostedService(_ =>
    {
        var evictionInterval = TimeSpan.FromMinutes(1);
        return new RateLimitBucketEvictionService(evictionInterval);
    });

    builder.Services.AddAuthorization(ProgramSetup.ConfigureAuthorizationPolicies);
    builder.Services.AddScoped<CalendarOwnerAccessEvaluator>();
    builder.Services.AddScoped<CurrentUserContextAccessor>();
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
    builder.Services.AddCascadingAuthenticationState();

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
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost |
                           ForwardedHeaders.XForwardedProto
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

    app.UseExceptionHandler(exceptionApp => exceptionApp.Run(ProgramSetup.HandleExceptionAsync));

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.OAuthClientId(swaggerOAuthClientId);
            options.OAuthUsePkce();
            options.OAuthScopes(swaggerOAuthScope);
            ProgramSetup.ConfigureSwaggerUi(options, swaggerOAuthRedirectUri);
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
        apiApp.Use(ProgramSetup.HandleApiRequestAsync);

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

