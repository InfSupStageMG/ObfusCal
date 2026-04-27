using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;
using ObfusCal.Application;
using ObfusCal.Application.Configuration;
using ObfusCal.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

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

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName))
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

    builder.Services.AddAuthorization();

    builder.Services
        .AddControllers()
        .AddJsonOptions(o =>
            o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

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
            [new OpenApiSecuritySchemeReference("OAuth2", null, null)] = [swaggerOAuthScope]
        });
    });
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.UseExceptionHandler(exceptionApp =>
    {
        exceptionApp.Run(async context =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();

            Log.ForContext("RequestMethod", context.Request.Method)
                .ForContext("RequestPath", exceptionFeature?.Path ?? context.Request.Path.Value)
                .ForContext("TraceId", context.TraceIdentifier)
                .Error(exceptionFeature?.Error, "Unhandled exception while processing HTTP request");

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

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

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
