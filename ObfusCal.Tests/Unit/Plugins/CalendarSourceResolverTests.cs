using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Plugins;

[TestClass]
public class CalendarSourceResolverTests
{
    private static readonly CalendarSourcePluginDescriptor AlphaDescriptor =
        new("alpha", "Alpha", typeof(AlphaCalendarSource), false);

    private static readonly CalendarSourcePluginDescriptor BetaDescriptor =
        new("beta", "Beta", typeof(BetaCalendarSource), false);

    // ---------------------------------------------------------------------------
    // Resolution priority: owner selection beats config
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ResolveAsync_UsesOwnerPluginId_WhenOwnerHasExplicitSelection()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Test",
            CalendarSourcePluginId = "beta"   // owner explicitly chose beta
        });
        await db.SaveChangesAsync();

        var resolver = Build(db, [AlphaDescriptor, BetaDescriptor], configuredProvider: "alpha");
        var source = await resolver.ResolveAsync(ownerId);

        Assert.IsInstanceOfType<BetaCalendarSource>(source,
            "Owner's saved selection should take precedence over configured default");
    }

    // ---------------------------------------------------------------------------
    // Resolution priority: config beats first-available when no owner selection
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ResolveAsync_UsesConfiguredProvider_WhenOwnerHasNoSelection()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner
        {
            Id = ownerId,
            Name = "Test",
            CalendarSourcePluginId = null   // no explicit selection
        });
        await db.SaveChangesAsync();

        var resolver = Build(db, [AlphaDescriptor, BetaDescriptor], configuredProvider: "beta");
        var source = await resolver.ResolveAsync(ownerId);

        Assert.IsInstanceOfType<BetaCalendarSource>(source,
            "Configured provider should be used when owner has no explicit selection");
    }

    // ---------------------------------------------------------------------------
    // Resolution priority: first-available fallback when config plugin not in catalog
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ResolveAsync_FallsBackToFirstPlugin_WhenConfiguredProviderNotInCatalog()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        // Catalog only has "alpha"; configured provider is "unknown" (not in catalog)
        var resolver = Build(db, [AlphaDescriptor], configuredProvider: "unknown");
        var source = await resolver.ResolveAsync(calendarOwnerId: null);

        Assert.IsInstanceOfType<AlphaCalendarSource>(source,
            "Should fall back to the first available plugin when configured provider is missing");
    }

    // ---------------------------------------------------------------------------
    // Owner not in DB → falls back to configured provider
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ResolveAsync_UsesConfiguredProvider_WhenOwnerIdNotInDatabase()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        // DB is empty — owner does not exist

        var resolver = Build(db, [AlphaDescriptor, BetaDescriptor], configuredProvider: "alpha");
        var source = await resolver.ResolveAsync(calendarOwnerId: Guid.NewGuid());

        Assert.IsInstanceOfType<AlphaCalendarSource>(source,
            "Should fall back to configured provider when owner is not in the database");
    }

    // ---------------------------------------------------------------------------
    // No owner id supplied → uses configured provider
    // ---------------------------------------------------------------------------

    [TestMethod]
    public async Task ResolveAsync_UsesConfiguredProvider_WhenNoOwnerIdProvided()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        var resolver = Build(db, [AlphaDescriptor, BetaDescriptor], configuredProvider: "beta");
        var source = await resolver.ResolveAsync(calendarOwnerId: null);

        Assert.IsInstanceOfType<BetaCalendarSource>(source);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static CalendarSourceResolver Build(
        AppDbContext db,
        IReadOnlyList<CalendarSourcePluginDescriptor> descriptors,
        string configuredProvider = "alpha",
        string environmentName = "Production")
    {
        var catalog = new CalendarSourcePluginCatalog(descriptors);

        var services = new ServiceCollection();
        services.AddTransient<AlphaCalendarSource>();
        services.AddTransient<BetaCalendarSource>();
        var sp = services.BuildServiceProvider();
        var instances = new FakeCalendarSourceInstanceService(ownerId => db.CalendarOwners.Any(owner => owner.Id == ownerId));

        return new CalendarSourceResolver(
            db,
            catalog,
            instances,
            sp,
            Options.Create(new CalendarSourceOptions { Provider = configuredProvider }),
            new FakeHostEnvironment(environmentName),
            NullLogger<AggregateCalendarSource>.Instance);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class AlphaCalendarSource : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from, DateTimeOffset to,
            Guid? calendarOwnerId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CalendarEvent>>([]);
    }

    private sealed class BetaCalendarSource : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from, DateTimeOffset to,
            Guid? calendarOwnerId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CalendarEvent>>([]);
    }
}

