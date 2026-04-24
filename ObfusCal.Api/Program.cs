using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services
        .Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName))
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services
        .AddControllers()
        .AddJsonOptions(o =>
            o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
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
        app.UseSwaggerUI();
    }

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
    Log.CloseAndFlush();
}
