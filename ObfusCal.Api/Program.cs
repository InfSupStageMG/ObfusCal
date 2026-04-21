using System.Runtime.Loader;
using ObfusCal.Core;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Obfuscation.Transformers;
using ObfusCal.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IShadowSlotStore, InMemoryShadowSlotStore>();

builder.Services.AddTransient<IObfuscationTransformer, RemoveTitleTransformer>();
builder.Services.AddTransient<IObfuscationTransformer, RemoveAttendeesTransformer>();
builder.Services.AddTransient<IObfuscationTransformer, RemoveDescriptionTransformer>();
builder.Services.AddTransient<IObfuscationTransformer, RemoveAttendeesTransformer>();
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
                Console.WriteLine($"[Plugins] Loaded Calendar Source: {type.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Plugins] Failed to load {dll}: {ex.Message}");
        }
    }
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();