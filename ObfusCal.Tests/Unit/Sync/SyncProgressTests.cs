using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Sync;

namespace ObfusCal.Tests.Unit.Sync;

[TestClass]
public class SyncProgressMonitorTests
{
    [TestMethod]
    public void IsPeerSyncInProgress_IsFalse_Initially()
    {
        var monitor = new SyncProgressMonitor();
        Assert.IsFalse(monitor.IsPeerSyncInProgress);
    }

    [TestMethod]
    public void LastPeerSyncCompletedAt_IsNull_Initially()
    {
        var monitor = new SyncProgressMonitor();
        Assert.IsNull(monitor.LastPeerSyncCompletedAt);
    }

    [TestMethod]
    public void TryBeginPeerSync_ReturnsTrue_WhenIdle()
    {
        var monitor = new SyncProgressMonitor();
        Assert.IsTrue(monitor.TryBeginPeerSync());
    }

    [TestMethod]
    public void TryBeginPeerSync_ReturnsFalse_WhenAlreadyRunning()
    {
        var monitor = new SyncProgressMonitor();
        monitor.TryBeginPeerSync();

        Assert.IsFalse(monitor.TryBeginPeerSync(), "Second TryBeginPeerSync should return false while first is still held.");
    }

    [TestMethod]
    public void IsPeerSyncInProgress_IsTrue_AfterBegin()
    {
        var monitor = new SyncProgressMonitor();
        monitor.TryBeginPeerSync();

        Assert.IsTrue(monitor.IsPeerSyncInProgress);
    }

    [TestMethod]
    public void IsPeerSyncInProgress_IsFalse_AfterEnd()
    {
        var monitor = new SyncProgressMonitor();
        monitor.TryBeginPeerSync();
        monitor.EndPeerSync();

        Assert.IsFalse(monitor.IsPeerSyncInProgress);
    }

    [TestMethod]
    public void LastPeerSyncCompletedAt_IsSet_AfterEnd()
    {
        var monitor = new SyncProgressMonitor();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var completedAtUtc = DateTimeOffset.UtcNow;

        monitor.TryBeginPeerSync();
        monitor.EndPeerSync(completedAtUtc);

        Assert.IsNotNull(monitor.LastPeerSyncCompletedAt);
        Assert.IsTrue(monitor.LastPeerSyncCompletedAt > before,
            "LastPeerSyncCompletedAt should be a recent UTC timestamp.");
    }

    [TestMethod]
    public void TryBeginPeerSync_Reacquirable_AfterEndSync()
    {
        var monitor = new SyncProgressMonitor();
        monitor.TryBeginPeerSync();
        monitor.EndPeerSync();

        Assert.IsTrue(monitor.TryBeginPeerSync(),
            "Lock should be re-acquirable after EndPeerSync.");
    }
}

[TestClass]
public class SyncProgressUpdateTests
{
    [TestMethod]
    public void IsIndeterminate_IsTrue_WhenTotalIsZero()
    {
        var update = new SyncProgressUpdate("Loading…", 0, 0);
        Assert.IsTrue(update.IsIndeterminate);
    }

    [TestMethod]
    public void IsIndeterminate_IsFalse_WhenTotalIsPositive()
    {
        var update = new SyncProgressUpdate("Step 1 of 5", 1, 5);
        Assert.IsFalse(update.IsIndeterminate);
    }

    [TestMethod]
    public void PercentComplete_IsNull_WhenIndeterminate()
    {
        var update = new SyncProgressUpdate("Loading…", 0, 0);
        Assert.IsNull(update.PercentComplete);
    }

    [TestMethod]
    public void PercentComplete_Is0_WhenCurrentIsZero()
    {
        var update = new SyncProgressUpdate("Starting", 0, 5);
        Assert.AreEqual(0, update.PercentComplete);
    }

    [TestMethod]
    public void PercentComplete_Is100_WhenCurrentEqualsTotal()
    {
        var update = new SyncProgressUpdate("Done", 5, 5);
        Assert.AreEqual(100, update.PercentComplete);
    }

    [TestMethod]
    public void PercentComplete_Is50_WhenHalfwayThrough()
    {
        var update = new SyncProgressUpdate("Halfway", 2, 4);
        Assert.AreEqual(50, update.PercentComplete);
    }
}

[TestClass]
public class PeerSyncProgressIntegrationTests
{
    [TestMethod]
    public async Task PeerSyncBackgroundService_ReleasesLock_AfterSuccessfulCycle()
    {
        var progressMonitor = new SyncProgressMonitor();
        var syncService = new ImmediateOutboundSyncService();
        var inboundService = new ImmediateInboundSyncService();

        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(syncService)
            .AddSingleton<IInboundPeerPullSyncService>(inboundService)
            .AddSingleton<IPeerSyncHistoryStore, InMemoryPeerSyncHistoryStore>()
            .BuildServiceProvider();

        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            progressMonitor,
            Options.Create(new SyncOptions { SyncIntervalSeconds = 60 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);

        // Wait for at least one cycle to complete
        await Task.WhenAny(syncService.InvocationObserved.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        // Give the finally block time to release
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Assert.IsFalse(progressMonitor.IsPeerSyncInProgress,
            "Lock must be released after a successful cycle.");
        Assert.IsNotNull(progressMonitor.LastPeerSyncCompletedAt,
            "LastPeerSyncCompletedAt must be set after a successful cycle.");

        await backgroundService.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task PeerSyncBackgroundService_ReleasesLock_AfterFailedCycle()
    {
        var progressMonitor = new SyncProgressMonitor();
        var throwingService = new ThrowingOutboundService();
        var inboundService = new ImmediateInboundSyncService();

        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(throwingService)
            .AddSingleton<IInboundPeerPullSyncService>(inboundService)
            .AddSingleton<IPeerSyncHistoryStore, InMemoryPeerSyncHistoryStore>()
            .BuildServiceProvider();

        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            progressMonitor,
            Options.Create(new SyncOptions { SyncIntervalSeconds = 60 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        Assert.IsFalse(progressMonitor.IsPeerSyncInProgress,
            "Lock must be released even when a sync cycle throws.");

        await backgroundService.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task PeerSyncBackgroundService_SkipsCycle_WhenLockAlreadyHeld()
    {
        var progressMonitor = new SyncProgressMonitor();
        // Pre-acquire the lock to simulate a concurrent caller
        progressMonitor.TryBeginPeerSync();

        var syncService = new ImmediateOutboundSyncService();
        var inboundService = new ImmediateInboundSyncService();

        await using var provider = new ServiceCollection()
            .AddSingleton<IOutboundPeerSyncService>(syncService)
            .AddSingleton<IInboundPeerPullSyncService>(inboundService)
            .AddSingleton<IPeerSyncHistoryStore, InMemoryPeerSyncHistoryStore>()
            .BuildServiceProvider();

        using var backgroundService = new PeerSyncBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            progressMonitor,
            Options.Create(new SyncOptions { SyncIntervalSeconds = 60 }),
            NullLogger<PeerSyncBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await backgroundService.StopAsync(CancellationToken.None);

        Assert.IsFalse(syncService.InvocationObserved.Task.IsCompleted,
            "Background service should skip the cycle when the lock is already held.");
    }

    private sealed class ImmediateOutboundSyncService : IOutboundPeerSyncService
    {
        public TaskCompletionSource InvocationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RunSyncCycleAsync(CancellationToken ct = default, IProgress<SyncProgressUpdate>? progress = null)
        {
            InvocationObserved.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class ImmediateInboundSyncService : IInboundPeerPullSyncService
    {
        public Task RunSyncCycleAsync(CancellationToken ct = default, IProgress<SyncProgressUpdate>? progress = null)
            => Task.CompletedTask;
    }

    private sealed class ThrowingOutboundService : IOutboundPeerSyncService
    {
        public Task RunSyncCycleAsync(CancellationToken ct = default, IProgress<SyncProgressUpdate>? progress = null)
            => throw new InvalidOperationException("Simulated failure");
    }

    private sealed class InMemoryPeerSyncHistoryStore : IPeerSyncHistoryStore
    {
        private DateTimeOffset? _lastCompletedAtUtc;

        public Task<DateTimeOffset?> GetLastCompletedAtUtcAsync(CancellationToken ct = default)
            => Task.FromResult(_lastCompletedAtUtc);

        public Task SetLastCompletedAtUtcAsync(DateTimeOffset completedAtUtc, CancellationToken ct = default)
        {
            _lastCompletedAtUtc = completedAtUtc.ToUniversalTime();
            return Task.CompletedTask;
        }
    }
}





