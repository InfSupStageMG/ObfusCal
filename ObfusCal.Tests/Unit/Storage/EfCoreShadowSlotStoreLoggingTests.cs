using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Storage;
using ObfusCal.Tests.Helpers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using BusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Tests.Unit.Storage;

[TestClass]
public class EfCoreShadowSlotStoreLoggingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task SetSlotsAsync_EmitsLogWithPeerIdAndCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, logger);

        await store.SetSlotsAsync("peer-x", [new BusySlot("e1", T0, T0.AddHours(1))]);

        var logEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Stored shadow slots for peer");
        Assert.AreEqual(LogEventLevel.Information, logEvent.Level);
        Assert.AreEqual("\"peer-x\"", logEvent.Properties["PeerId"].ToString());
        Assert.AreEqual("1", logEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task SetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdOwnerIdAndCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, logger);
        var ownerId = Guid.NewGuid();

        await store.SetSlotsAsync("peer-y", ownerId, [new BusySlot("e1", T0, T0.AddHours(1))]);

        var logEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Stored owner-scoped shadow slots for peer");
        Assert.AreEqual(LogEventLevel.Information, logEvent.Level);
        Assert.AreEqual("\"peer-y\"", logEvent.Properties["PeerId"].ToString());
        Assert.AreEqual(ownerId.ToString(), logEvent.Properties["CalendarOwnerId"].ToString());
        Assert.AreEqual("1", logEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task GetSlotsAsync_EmitsLogWithPeerIdAndCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, logger);

        await store.SetSlotsAsync("peer-z", [new BusySlot("e1", T0, T0.AddHours(1))]);
        _ = await store.GetSlotsAsync("peer-z");

        var logEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Read shadow slots for peer");
        Assert.AreEqual(LogEventLevel.Debug, logEvent.Level);
        Assert.AreEqual("\"peer-z\"", logEvent.Properties["PeerId"].ToString());
        Assert.AreEqual("1", logEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task GetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdOwnerIdAndCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, logger);
        var ownerId = Guid.NewGuid();

        await store.SetSlotsAsync("peer-w", ownerId, [new BusySlot("e1", T0, T0.AddHours(1))]);
        _ = await store.GetSlotsAsync("peer-w", ownerId);

        var logEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Read owner-scoped shadow slots for peer");
        Assert.AreEqual(LogEventLevel.Debug, logEvent.Level);
        Assert.AreEqual("\"peer-w\"", logEvent.Properties["PeerId"].ToString());
        Assert.AreEqual(ownerId.ToString(), logEvent.Properties["CalendarOwnerId"].ToString());
        Assert.AreEqual("1", logEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_EmitsLogWithSlotCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, logger);

        await store.SetSlotsAsync("peer-v", [new BusySlot("e1", T0, T0.AddHours(1))]);
        _ = await store.GetAllSlotsAsync(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        var logEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Read all shadow slots from all peers");
        Assert.AreEqual(LogEventLevel.Debug, logEvent.Level);
        Assert.AreEqual("1", logEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task GetAllSlotsAsync_OwnerScoped_EmitsLogWithOwnerIdAndSlotCount()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, logger);
        var ownerId = Guid.NewGuid();

        await SeedActiveMappingAsync(db, ownerId, "peer-u");

        await store.SetSlotsAsync("peer-u", ownerId, [new BusySlot("e1", T0, T0.AddHours(1))]);
        _ = await store.GetAllSlotsAsync(ownerId, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

        var logEvent = sink.Events.Single(e => e.MessageTemplate.Text == "Read owner-scoped shadow slots from all peers");
        Assert.AreEqual(LogEventLevel.Debug, logEvent.Level);
        Assert.AreEqual(ownerId.ToString(), logEvent.Properties["CalendarOwnerId"].ToString());
        Assert.AreEqual("1", logEvent.Properties["BusySlotCount"].ToString());
    }

    [TestMethod]
    public async Task SetSlotsAsync_WithWhitespacePeerId_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SetSlotsAsync("   ", []));
    }

    [TestMethod]
    public async Task SetSlotsAsync_OwnerScoped_WithWhitespacePeerId_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SetSlotsAsync("   ", Guid.NewGuid(), []));
    }

    [TestMethod]
    public async Task GetSlotsAsync_WithWhitespacePeerId_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.GetSlotsAsync("   "));
    }

    [TestMethod]
    public async Task GetSlotsAsync_OwnerScoped_WithWhitespacePeerId_Throws()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var store = new EfCoreShadowSlotStore(db, Serilog.Core.Logger.None);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.GetSlotsAsync("   ", Guid.NewGuid()));
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        void ILogEventSink.Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static async Task SeedActiveMappingAsync(AppDbContext db, Guid ownerId, string peerId)
    {
        if (!await db.CalendarOwners.AnyAsync(owner => owner.Id == ownerId))
        {
            db.CalendarOwners.Add(new CalendarOwner
            {
                Id = ownerId,
                Name = "Log Test Owner"
            });
        }

        var peer = await db.PeerConnections.SingleOrDefaultAsync(p => p.InstanceId == peerId);
        if (peer is null)
        {
            peer = new PeerConnection
            {
                Id = Guid.NewGuid(),
                InstanceId = peerId,
                BaseAddress = "https://peer.local/",
                ApiKeyHash = "hash",
                Status = PeerConnectionStatus.Active
            };
            db.PeerConnections.Add(peer);
        }

        var mappingExists = await db.CalendarOwnerPeerMappings.AnyAsync(mapping =>
            mapping.CalendarOwnerId == ownerId && mapping.PeerConnectionId == peer.Id);
        if (!mappingExists)
        {
            db.CalendarOwnerPeerMappings.Add(new CalendarOwnerPeerMapping
            {
                Id = Guid.NewGuid(),
                CalendarOwnerId = ownerId,
                PeerConnectionId = peer.Id,
                CalendarOwnerRef = Guid.NewGuid()
            });
        }

        await db.SaveChangesAsync();
    }
}

