using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Storage;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ObfusCal.Tests.Unit.Storage;

[TestClass]
public class InMemoryShadowSlotStoreLoggingTests
{
    [TestMethod]
    public async Task SetAndGetSlots_EmitStructuredLogs_WithPeerIdAndCountOnly()
    {
        var sink = new CollectingSink();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var store = new InMemoryShadowSlotStore(logger);
        var slot = new BusySlot("evt-sensitive-id", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30));

        await store.SetSlotsAsync("peer-a", [slot], TestContext.CancellationToken);
        _ = await store.GetSlotsAsync("peer-a", TestContext.CancellationToken);

        var writeEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Stored shadow slots for peer");
        var readEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Read shadow slots for peer");

        Assert.AreEqual(LogEventLevel.Information, writeEvent.Level);
        Assert.AreEqual(LogEventLevel.Debug, readEvent.Level);

        Assert.AreEqual("\"peer-a\"", writeEvent.Properties["PeerId"].ToString());
        Assert.AreEqual("1", writeEvent.Properties["BusySlotCount"].ToString());
        Assert.AreEqual("\"peer-a\"", readEvent.Properties["PeerId"].ToString());
        Assert.AreEqual("1", readEvent.Properties["BusySlotCount"].ToString());

        Assert.IsFalse(writeEvent.RenderMessage().Contains("evt-sensitive-id", StringComparison.Ordinal));
        Assert.IsFalse(readEvent.RenderMessage().Contains("evt-sensitive-id", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdAndOwnerIdAndCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var store = new InMemoryShadowSlotStore(logger);
        var ownerId = Guid.NewGuid();

        await store.SetSlotsAsync("peer-b", ownerId, [
            new BusySlot("evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(15)),
            new BusySlot("evt-2", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30))
        ]);

        var logEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Stored owner-scoped shadow slots for peer");
        Assert.AreEqual(LogEventLevel.Information, logEvent.Level);
        Assert.AreEqual("\"peer-b\"", logEvent.Properties["PeerId"].ToString());
        Assert.AreEqual(ownerId.ToString(), logEvent.Properties["CalendarOwnerId"].ToString());
        Assert.AreEqual("2", logEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task GetSlotsAsync_EmitsLogWithPeerIdAndCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var store = new InMemoryShadowSlotStore(logger);

        await store.SetSlotsAsync("peer-c", [new BusySlot("evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30))]);
        _ = await store.GetSlotsAsync("peer-c");

        var readEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Read shadow slots for peer");
        Assert.AreEqual("\"peer-c\"", readEvent.Properties["PeerId"].ToString());
        Assert.AreEqual("1", readEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task GetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdOwnerIdAndCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var store = new InMemoryShadowSlotStore(logger);
        var ownerId = Guid.NewGuid();

        await store.SetSlotsAsync("peer-d", ownerId, [new BusySlot("evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30))]);
        _ = await store.GetSlotsAsync("peer-d", ownerId);

        var readEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Read owner-scoped shadow slots for peer");
        Assert.AreEqual(LogEventLevel.Debug, readEvent.Level);
        Assert.AreEqual("\"peer-d\"", readEvent.Properties["PeerId"].ToString());
        Assert.AreEqual(ownerId.ToString(), readEvent.Properties["CalendarOwnerId"].ToString());
        Assert.AreEqual("1", readEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_EmitsLogWithPeerCountAndSlotCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var store = new InMemoryShadowSlotStore(logger);

        await store.SetSlotsAsync("peer-e", [new BusySlot("evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);
        await store.SetSlotsAsync("peer-f", [new BusySlot("evt-2", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        _ = await store.GetAllSlotsAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        var readEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Read all shadow slots from all peers");
        Assert.AreEqual(LogEventLevel.Debug, readEvent.Level);
        Assert.AreEqual("2", readEvent.Properties["PeerCount"].ToString());
        Assert.AreEqual("2", readEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_EmitsLogWithOwnerIdAndSlotCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var store = new InMemoryShadowSlotStore(logger);
        var ownerId = Guid.NewGuid();

        await store.SetSlotsAsync("peer-g", ownerId, [new BusySlot("evt-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))]);

        _ = await store.GetAllSlotsAsync(ownerId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        var readEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Read owner-scoped shadow slots from all peers");
        Assert.AreEqual(LogEventLevel.Debug, readEvent.Level);
        Assert.AreEqual(ownerId.ToString(), readEvent.Properties["CalendarOwnerId"].ToString());
        Assert.AreEqual("1", readEvent.Properties["BusySlotCount"].ToString());
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        void ILogEventSink.Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
