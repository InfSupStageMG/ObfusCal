using ObfusCal.Core.Models;
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

    public TestContext TestContext { get; set; } = null!;

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        void ILogEventSink.Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
