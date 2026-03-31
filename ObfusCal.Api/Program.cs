using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Obfuscation;
using ObfusCal.Core.Obfuscation.Transformers;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Peers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ICalendarSource, MockCalendarSource>(); // swap to GraphCalendarSource later
builder.Services.AddScoped<IEventTransformer, StripTitleTransformer>();
builder.Services.AddScoped<IEventTransformer, RemoveAttendeesTransformer>();
builder.Services.AddScoped<ObfuscationPipeline>();
builder.Services.AddHttpClient<IPeerClient, HttpPeerClient>();