using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Sync;

namespace ObfusCal.Tests.Unit.Sync;

[TestClass]
public class PeerSyncBackgroundServiceTests
{
    [TestMethod]
    public async Task StartAsync_InvokesOutboundSyncService()
    {
        var countingService = new CountingOutboundPeerSyncService();
        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(countingService)
            .BuildServiceProvider();

        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncOptions { SyncIntervalSeconds = 60 }),
            new NullLogger<PeerSyncBackgroundService>());

        await backgroundService.StartAsync(CancellationToken.None);

        var completedTask = await Task.WhenAny(countingService.InvocationObserved.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(countingService.InvocationObserved.Task, completedTask);

        await backgroundService.StopAsync(CancellationToken.None);
    }

    private sealed class CountingOutboundPeerSyncService : IOutboundPeerSyncService
    {
        public TaskCompletionSource InvocationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RunSyncCycleAsync(CancellationToken ct = default)
        {
            InvocationObserved.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}

