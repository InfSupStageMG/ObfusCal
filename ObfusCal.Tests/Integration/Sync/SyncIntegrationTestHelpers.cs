using Microsoft.Extensions.Logging;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Sync;

internal static class SyncIntegrationTestHelpers
{
    internal static AppDbContext CreateDbContext() => TestDbContextFactory.CreateInMemory();

    /// <summary>
    /// Seeds a CalendarOwner (if not already present), a PeerConnection, and a CalendarOwnerPeerMapping.
    /// Returns the ID of the created PeerConnection.
    /// </summary>
    internal static Guid SeedOwnerAndPeerMapping(
        AppDbContext dbContext,
        Guid calendarOwnerId,
        Guid calendarOwnerRef,
        string peerInstanceId,
        string baseAddress)
    {
        if (!dbContext.CalendarOwners.Any(owner => owner.Id == calendarOwnerId))
        {
            dbContext.CalendarOwners.Add(new CalendarOwner
            {
                Id = calendarOwnerId,
                Name = "Owner"
            });
        }

        var peerConnectionId = Guid.NewGuid();
        dbContext.PeerConnections.Add(new PeerConnection
        {
            Id = peerConnectionId,
            InstanceId = peerInstanceId,
            BaseAddress = baseAddress,
            ApiKeyHash = "hashed-not-used-here"
        });

        dbContext.CalendarOwnerPeerMappings.Add(new CalendarOwnerPeerMapping
        {
            Id = Guid.NewGuid(),
            CalendarOwnerId = calendarOwnerId,
            PeerConnectionId = peerConnectionId,
            CalendarOwnerRef = calendarOwnerRef
        });

        dbContext.SaveChanges();
        return peerConnectionId;
    }
}

internal sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal sealed class DelegatingHttpMessageHandler(
    Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => handler(request);
}

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}

internal sealed record LogEntry(LogLevel LogLevel, string Message);


