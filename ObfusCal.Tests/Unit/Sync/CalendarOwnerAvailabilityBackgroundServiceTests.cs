using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Sync;

namespace ObfusCal.Tests.Unit.Sync;

[TestClass]
public class CalendarOwnerAvailabilityBackgroundServiceTests
{
    [TestMethod]
    public async Task StartAsync_InvokesCalendarOwnerAvailabilitySyncService()
    {
        var countingService = new CountingCalendarOwnerAvailabilitySyncService();
        await using var provider = new ServiceCollection()
            .AddSingleton<ICalendarOwnerAvailabilitySyncService>(countingService)
            .BuildServiceProvider();

        using var backgroundService = new CalendarOwnerAvailabilityBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncOptions { SyncIntervalSeconds = 60 }),
            NullLogger<CalendarOwnerAvailabilityBackgroundService>.Instance);

        await backgroundService.StartAsync(CancellationToken.None);

        var completedTask = await Task.WhenAny(countingService.InvocationObserved.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(countingService.InvocationObserved.Task, completedTask);

        await backgroundService.StopAsync(CancellationToken.None);
    }

    private sealed class CountingCalendarOwnerAvailabilitySyncService : ICalendarOwnerAvailabilitySyncService
    {
        public TaskCompletionSource InvocationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RunSyncCycleAsync(CancellationToken ct = default)
        {
            InvocationObserved.TrySetResult();
            return Task.CompletedTask;
        }
    }
}

