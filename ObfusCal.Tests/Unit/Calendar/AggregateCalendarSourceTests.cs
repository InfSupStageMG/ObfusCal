using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Application;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Calendar;

[TestClass]
public class AggregateCalendarSourceTests
{
    [TestMethod]
    public async Task WriteBackSlotsAsync_WritesOtherSourceBusySlotsToEachWritableDestination()
    {
        var ownerId = Guid.NewGuid();
        var instanceService = new FakeCalendarSourceInstanceService();

        var googleSummary = await instanceService.CreateAsync(ownerId, new CreateCalendarSourceInstanceInput("google-test", "Google"));
        var graphSummary = await instanceService.CreateAsync(ownerId, new CreateCalendarSourceInstanceInput("graph-test", "Graph"));
        Assert.IsNotNull(googleSummary);
        Assert.IsNotNull(graphSummary);

        var googleInstance = await instanceService.GetAsync(ownerId, googleSummary.Id);
        var graphInstance = await instanceService.GetAsync(ownerId, graphSummary.Id);
        Assert.IsNotNull(googleInstance);
        Assert.IsNotNull(graphInstance);

        var googleEvent = new CalendarEvent(
            "google-1",
            "Google event",
            null,
            new DateTimeOffset(2026, 5, 18, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 18, 10, 0, 0, TimeSpan.Zero),
            [],
            null);
        var graphEvent = new CalendarEvent(
            "graph-1",
            "Graph event",
            null,
            new DateTimeOffset(2026, 5, 18, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
            [],
            null);
        var shadowSlot = new BusySlot(
            "peer-1",
            new DateTimeOffset(2026, 5, 18, 13, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 18, 14, 0, 0, TimeSpan.Zero));

        var googleSource = new GoogleWritableTestSource([googleEvent]);
        var graphSource = new GraphWritableTestSource([graphEvent]);

        using var applicationServices = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .BuildServiceProvider();
        using var sourceServices = new ServiceCollection()
            .AddSingleton(googleSource)
            .AddSingleton(graphSource)
            .BuildServiceProvider();

        var aggregate = new AggregateCalendarSource(
            ownerId,
            new FakeCatalog(
                new CalendarSourcePluginDescriptor("google-test", "Google", typeof(GoogleWritableTestSource), false),
                new CalendarSourcePluginDescriptor("graph-test", "Graph", typeof(GraphWritableTestSource), false)),
            instanceService,
            sourceServices,
            applicationServices.GetRequiredService<ObfuscationPipeline>(),
            new StubCalendarOwnerObfuscationProfileService(),
            NullLogger<AggregateCalendarSource>.Instance);

        await aggregate.WriteBackSlotsAsync(
            ownerId,
            [shadowSlot],
            "Busy",
            new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero));

        Assert.HasCount(1, googleSource.Writes);
        Assert.HasCount(1, graphSource.Writes);
        Assert.AreEqual(googleInstance.Id, googleSource.Writes[0].InstanceId);
        Assert.AreEqual(graphInstance.Id, graphSource.Writes[0].InstanceId);
        Assert.AreEqual("Busy", googleSource.Writes[0].PlaceholderTitle);
        Assert.AreEqual("Busy", graphSource.Writes[0].PlaceholderTitle);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.Zero), googleSource.Writes[0].WindowStart);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero), googleSource.Writes[0].WindowEnd);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.Zero), graphSource.Writes[0].WindowStart);
        Assert.AreEqual(new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero), graphSource.Writes[0].WindowEnd);

        CollectionAssert.AreEquivalent(
            new[]
            {
                $"{graphInstance.Id:N}:graph-1",
                shadowSlot.SourceEventId
            },
            googleSource.Writes[0].BusySlots.Select(slot => slot.SourceEventId).ToArray());
        CollectionAssert.AreEquivalent(
            new[]
            {
                $"{googleInstance.Id:N}:google-1",
                shadowSlot.SourceEventId
            },
            graphSource.Writes[0].BusySlots.Select(slot => slot.SourceEventId).ToArray());
    }

    private sealed class StubCalendarOwnerObfuscationProfileService : ICalendarOwnerObfuscationProfileService
    {
        public Task<IReadOnlyList<ObfuscationProfileSettings>> GetProfilesAsync(Guid calendarOwnerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ObfuscationProfileSettings>>(
                Enum.GetValues<ObfuscationAuditContext>().Select(ObfuscationProfileSettings.CreateDefault).ToList());

        public Task<ObfuscationProfileSettings> GetProfileAsync(Guid calendarOwnerId, ObfuscationAuditContext context, CancellationToken ct = default)
            => Task.FromResult(ObfuscationProfileSettings.CreateDefault(context));

        public Task<ObfuscationProfileSettings> SetProfileAsync(Guid calendarOwnerId, ObfuscationProfileSettings profile, CancellationToken ct = default)
            => Task.FromResult(profile);
    }

    private sealed class FakeCatalog(params CalendarSourcePluginDescriptor[] plugins) : ICalendarSourceCatalog
    {
        public IReadOnlyList<CalendarSourcePluginDescriptor> GetPlugins() => plugins;

        public CalendarSourcePluginDescriptor? GetPlugin(string pluginId)
            => plugins.SingleOrDefault(plugin => string.Equals(plugin.Id, pluginId, StringComparison.OrdinalIgnoreCase));
    }

    private abstract class WritableTestSource(IReadOnlyList<CalendarEvent> events)
        : ICalendarSource, ICalendarSourceInstanceHandler, ICalendarSourceInstanceWriteBack
    {
        public List<WriteBackCall> Writes { get; } = [];

        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? calendarOwnerId = null,
            CancellationToken ct = default)
            => Task.FromResult(events);

        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            CalendarSourceInstanceContext instance,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken ct = default)
            => Task.FromResult(events);

        public Task WriteBackSlotsAsync(
            CalendarSourceInstanceContext instance,
            IReadOnlyList<BusySlot> busySlots,
            string placeholderTitle,
            DateTimeOffset windowStart,
            DateTimeOffset windowEnd,
            CancellationToken ct = default)
        {
            Writes.Add(new WriteBackCall(instance.Id, busySlots.ToList(), placeholderTitle, windowStart, windowEnd));
            return Task.CompletedTask;
        }
    }

    private sealed class GoogleWritableTestSource(IReadOnlyList<CalendarEvent> events) : WritableTestSource(events);

    private sealed class GraphWritableTestSource(IReadOnlyList<CalendarEvent> events) : WritableTestSource(events);

    private sealed record WriteBackCall(
        Guid InstanceId,
        IReadOnlyList<BusySlot> BusySlots,
        string PlaceholderTitle,
        DateTimeOffset WindowStart,
        DateTimeOffset WindowEnd);
}


