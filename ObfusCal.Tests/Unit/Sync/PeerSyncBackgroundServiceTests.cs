using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Sync;

namespace ObfusCal.Tests.Unit.Sync;

[TestClass]
public class PeerSyncBackgroundServiceTests
{
    [TestMethod]
    public async Task StartAsync_InvokesOutboundAndInboundSyncServices()
    {
        var countingService = new CountingOutboundPeerSyncService();
        var countingInboundService = new CountingInboundPeerPullSyncService();
        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(countingService)
            .AddSingleton<IInboundPeerPullSyncService>(countingInboundService)
            .BuildServiceProvider();

        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncOptions { SyncIntervalSeconds = 60 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);

        var completedTask = await Task.WhenAny(countingService.InvocationObserved.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(countingService.InvocationObserved.Task, completedTask);

        var inboundCompletedTask = await Task.WhenAny(countingInboundService.InvocationObserved.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(countingInboundService.InvocationObserved.Task, inboundCompletedTask);

        await backgroundService.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinuesAfterSyncFailure()
    {
        var throwingOutbound = new ThrowingOutboundPeerSyncService();
        var countingInbound = new CountingInboundPeerPullSyncService();
        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(throwingOutbound)
            .AddSingleton<IInboundPeerPullSyncService>(countingInbound)
            .BuildServiceProvider();

        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncOptions { SyncIntervalSeconds = 60 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);

        // Give it time to run at least one cycle
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await backgroundService.StopAsync(CancellationToken.None);

        // Service should not have crashed — the catch block should handle the failure
        Assert.IsTrue(throwingOutbound.WasInvoked, "Outbound sync should have been invoked even if it throws");
    }

    [TestMethod]
    public async Task ExecuteAsync_UsesConfiguredInterval_ClampedToMinimum()
    {
        var countingService = new CountingOutboundPeerSyncService();
        var countingInbound = new CountingInboundPeerPullSyncService();
        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(countingService)
            .AddSingleton<IInboundPeerPullSyncService>(countingInbound)
            .BuildServiceProvider();

        // Use a very small interval (0) — it should be clamped to at least 1 second
        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncOptions { SyncIntervalSeconds = 0 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);

        var completedTask = await Task.WhenAny(countingService.InvocationObserved.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.AreSame(countingService.InvocationObserved.Task, completedTask,
            "Even with interval=0, the service should execute at least once");

        await backgroundService.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task StopAsync_StopsGracefully()
    {
        var countingService = new CountingOutboundPeerSyncService();
        var countingInbound = new CountingInboundPeerPullSyncService();
        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(countingService)
            .AddSingleton<IInboundPeerPullSyncService>(countingInbound)
            .BuildServiceProvider();

        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncOptions { SyncIntervalSeconds = 3600 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);

        // Wait for at least one execution
        var completed = await Task.WhenAny(countingService.InvocationObserved.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(countingService.InvocationObserved.Task, completed);

        // Stop should not throw
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

    private sealed class CountingInboundPeerPullSyncService : IInboundPeerPullSyncService
    {
        public TaskCompletionSource InvocationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RunSyncCycleAsync(CancellationToken ct = default)
        {
            InvocationObserved.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingOutboundPeerSyncService : IOutboundPeerSyncService
    {
        public bool WasInvoked { get; private set; }

        public Task RunSyncCycleAsync(CancellationToken ct = default)
        {
            WasInvoked = true;
            throw new InvalidOperationException("Simulated sync failure");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNegativeInterval_ClampsToOneSecond()
    {
        var countingService = new CountingOutboundPeerSyncService();
        var countingInbound = new CountingInboundPeerPullSyncService();
        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(countingService)
            .AddSingleton<IInboundPeerPullSyncService>(countingInbound)
            .BuildServiceProvider();

        // Negative interval should be clamped to at least 1 (Math.Max(1, -5) = 1)
        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncOptions { SyncIntervalSeconds = -5 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);

        var completedTask = await Task.WhenAny(countingService.InvocationObserved.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.AreSame(countingService.InvocationObserved.Task, completedTask,
            "Service should execute at least once even with negative interval");

        await backgroundService.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task ExecuteAsync_RunsMultipleCycles_WithSmallInterval()
    {
        var invocationCount = 0;
        var twoInvocations = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var multiCountOutbound = new MultiCountOutboundPeerSyncService(() =>
        {
            if (Interlocked.Increment(ref invocationCount) >= 2)
                twoInvocations.TrySetResult();
        });
        var countingInbound = new CountingInboundPeerPullSyncService();
        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(multiCountOutbound)
            .AddSingleton<IInboundPeerPullSyncService>(countingInbound)
            .BuildServiceProvider();

        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncOptions { SyncIntervalSeconds = 1 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);

        var completedTask = await Task.WhenAny(twoInvocations.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.AreSame(twoInvocations.Task, completedTask,
            "Service should execute at least 2 cycles with 1-second interval");

        await backgroundService.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithLargeInterval_DoesNotRunSecondCycleQuickly()
    {
        var invocationCount = 0;
        var secondInvocation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var multiCountOutbound = new MultiCountOutboundPeerSyncService(() =>
        {
            if (Interlocked.Increment(ref invocationCount) >= 2)
                secondInvocation.TrySetResult();
        });
        var countingInbound = new CountingInboundPeerPullSyncService();
        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(multiCountOutbound)
            .AddSingleton<IInboundPeerPullSyncService>(countingInbound)
            .BuildServiceProvider();

        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncOptions { SyncIntervalSeconds = 60 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);

        // With 60s interval, the second invocation should NOT happen within 2 seconds
        var completedTask = await Task.WhenAny(secondInvocation.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreNotSame(secondInvocation.Task, completedTask,
            "With 60s interval, second cycle should not fire within 2 seconds");

        await backgroundService.StopAsync(CancellationToken.None);
    }

    private sealed class MultiCountOutboundPeerSyncService(Action onInvoke) : IOutboundPeerSyncService
    {
        public Task RunSyncCycleAsync(CancellationToken ct = default)
        {
            onInvoke();
            return Task.CompletedTask;
        }
    }
}
