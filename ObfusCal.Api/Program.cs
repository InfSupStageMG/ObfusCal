using Microsoft.Extensions.Options;
using ObfusCal.Core.Configuration;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Obfuscation;
using ObfusCal.Core.Obfuscation.Transformers;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Peers;
using ObfusCal.Infrastructure.Storage;
using ObfusCal.Sync;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<SyncOptions>(
    builder.Configuration.GetSection(SyncOptions.Section));

builder.Services.AddScoped<ICalendarSource, MockCalendarSource>();

builder.Services.AddScoped<IEventTransformer, StripTitleTransformer>();
builder.Services.AddScoped<IEventTransformer, RemoveAttendeesTransformer>();
builder.Services.AddScoped<IEventTransformer, RoundTimesTransformer>();
builder.Services.AddScoped<ObfuscationPipeline>();

builder.Services.AddSingleton<IShadowSlotStore, InMemoryShadowSlotStore>();

builder.Services.AddHttpClient<IPeerClient, HttpPeerClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<SyncOptions>>().Value;
    client.DefaultRequestHeaders.Add("X-Peer-Id", opts.InstanceId);
});

builder.Services.AddHostedService<SyncService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();