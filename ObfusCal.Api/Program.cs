using System.Runtime.Loader;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ObfusCal.Core;
using ObfusCal.Core.Configuration;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Obfuscation.Transformers;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Storage;
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

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHealthChecks();
    builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

    builder.Services.AddScoped<IShadowSlotStore, EfCoreShadowSlotStore>();

    builder.Services.AddTransient<IObfuscationTransformer, RemoveTitleTransformer>();
    builder.Services.AddTransient<IObfuscationTransformer, RemoveDescriptionTransformer>();
    builder.Services.AddTransient<IObfuscationTransformer, RemoveLocationTransformer>();
    builder.Services.AddTransient<IObfuscationTransformer, RemoveAttendeesTransformer>();
    builder.Services.AddTransient<IObfuscationTransformer, RoundTimesTransformer>();
    builder.Services.AddTransient<IBusySlotTransformer, MergeBlocksTransformer>();
    builder.Services.AddTransient<ObfuscationPipeline>();

    var pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");

    if (Directory.Exists(pluginFolder))
    {
        foreach (var dll in Directory.GetFiles(pluginFolder, "*.dll"))
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);

                var calendarSources = assembly.GetTypes()
                    .Where(t => typeof(ICalendarSource).IsAssignableFrom(t)
                                && t is { IsInterface: false, IsAbstract: false });

                foreach (var type in calendarSources)
                {
                    builder.Services.AddScoped(typeof(ICalendarSource), type);
                    Log.ForContext("CalendarSourceType", type.Name)
                        .ForContext("PluginAssemblyPath", dll)
                        .Information("Loaded calendar source plugin");
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("PluginAssemblyPath", dll)
                    .Error(ex, "Failed to load plugin assembly");
            }
        }
    }

    builder.Services.AddScoped<ICalendarSource, MockCalendarSource>();

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
