using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Tests.Unit.Plugins;

[TestClass]
public class PluginAllowlistTests
{
    [TestMethod]
    public void Discover_AllowsBuiltInPlugins_WhenAllowlistEnabled()
    {
        var allowlist = new PluginAllowlistOptions
        {
            Enabled = true,
            AllowedPluginIds = []
        };

        var plugins = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false, allowlist);
        var ids = plugins.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(ids.Contains("graph"), "Expected 'graph' built-in plugin");
        Assert.IsTrue(ids.Contains("ical"), "Expected 'ical' built-in plugin");
        Assert.IsTrue(ids.Contains("mock"), "Expected 'mock' built-in plugin");
    }

    [TestMethod]
    public void Discover_AllowsBuiltInPlugins_WhenAllowlistDisabled()
    {
        var allowlist = new PluginAllowlistOptions { Enabled = false };

        var plugins = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false, allowlist);
        var ids = plugins.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(ids.Contains("graph"), "Expected 'graph' plugin when allowlist disabled");
    }

    [TestMethod]
    public void Discover_DoesNotThrow_WhenAllowlistOptionsIsNull()
    {
        var result = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false, allowlist: null);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void Discover_DoesNotThrow_WhenSomeAssemblyTypesCannotBeLoaded()
    {
        IReadOnlyList<CalendarSourcePluginDescriptor>? result = null;
        Exception? thrown = null;
        try
        {
            result = CalendarSourcePluginCatalog.Discover(
                includeExternalPlugins: false,
                allowlist: null,
                logger: NullLogger.Instance);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.IsNull(thrown, $"Discover() must not propagate exceptions; threw: {thrown?.Message}");
        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result, "At least the built-in plugins should always be discovered");
    }

    [TestMethod]
    public void Cache_IsNotInitialized_BeforeInitializeIsCalled()
    {
        var cache = new PluginAllowlistCache();
        Assert.IsFalse(cache.IsInitialized);
        Assert.AreEqual(0, cache.GetBlockedPluginIds().Count);
    }

    [TestMethod]
    public void Cache_AfterInitialize_ContainsBlockedIds()
    {
        var cache = new PluginAllowlistCache();
        cache.Initialize(["google", "icloud"]);

        Assert.IsTrue(cache.IsInitialized);
        Assert.IsTrue(cache.GetBlockedPluginIds().Contains("google"));
        Assert.IsTrue(cache.GetBlockedPluginIds().Contains("icloud"));
        Assert.IsFalse(cache.GetBlockedPluginIds().Contains("graph"));
    }

    [TestMethod]
    public void Cache_IsCaseInsensitive()
    {
        var cache = new PluginAllowlistCache();
        cache.Initialize(["Google"]);

        Assert.IsTrue(cache.GetBlockedPluginIds().Contains("google"));
        Assert.IsTrue(cache.GetBlockedPluginIds().Contains("GOOGLE"));
    }

    [TestMethod]
    public void Cache_MarkBlocked_AddsEntry()
    {
        var cache = new PluginAllowlistCache();
        cache.Initialize([]);

        cache.MarkBlocked("graph");
        Assert.IsTrue(cache.GetBlockedPluginIds().Contains("graph"));
    }

    [TestMethod]
    public void Cache_MarkAllowed_RemovesEntry()
    {
        var cache = new PluginAllowlistCache();
        cache.Initialize(["graph"]);

        cache.MarkAllowed("graph");
        Assert.IsFalse(cache.GetBlockedPluginIds().Contains("graph"));
    }

    [TestMethod]
    public void GetPlugins_FiltersBlockedPlugins_ViaCache()
    {
        var cache = new PluginAllowlistCache();
        cache.Initialize(["beta"]);

        var catalog = new CalendarSourcePluginCatalog(
        [
            new CalendarSourcePluginDescriptor("alpha", "Alpha", typeof(StubSource), false),
            new CalendarSourcePluginDescriptor("beta", "Beta", typeof(StubSource), false)
        ], cache);

        var ids = catalog.GetPlugins().Select(p => p.Id).ToArray();

        CollectionAssert.Contains(ids, "alpha");
        CollectionAssert.DoesNotContain(ids, "beta");
    }

    [TestMethod]
    public void GetPlugin_ReturnsNull_ForBlockedPlugin()
    {
        var cache = new PluginAllowlistCache();
        cache.Initialize(["beta"]);

        var catalog = new CalendarSourcePluginCatalog(
        [
            new CalendarSourcePluginDescriptor("beta", "Beta", typeof(StubSource), false)
        ], cache);

        Assert.IsNull(catalog.GetPlugin("beta"),
            "A blocked plugin should not be returned by GetPlugin");
    }

    [TestMethod]
    public void GetPlugin_ReturnsPlugin_WhenNotBlocked()
    {
        var cache = new PluginAllowlistCache();
        cache.Initialize([]);

        var catalog = new CalendarSourcePluginCatalog(
        [
            new CalendarSourcePluginDescriptor("alpha", "Alpha", typeof(StubSource), false)
        ], cache);

        Assert.IsNotNull(catalog.GetPlugin("alpha"));
    }

    [TestMethod]
    public void GetPlugins_ReturnsAll_WhenCacheIsNull()
    {
        var catalog = new CalendarSourcePluginCatalog(
        [
            new CalendarSourcePluginDescriptor("alpha", "Alpha", typeof(StubSource), false),
            new CalendarSourcePluginDescriptor("beta", "Beta", typeof(StubSource), false)
        ]);

        Assert.AreEqual(2, catalog.GetPlugins().Count);
    }

    [TestMethod]
    public void GetPlugins_ReflectsRuntimeToggle_ImmediatelyAfterCacheUpdate()
    {
        var cache = new PluginAllowlistCache();
        cache.Initialize([]);

        var catalog = new CalendarSourcePluginCatalog(
        [
            new CalendarSourcePluginDescriptor("alpha", "Alpha", typeof(StubSource), false)
        ], cache);

        Assert.AreEqual(1, catalog.GetPlugins().Count, "Plugin should be visible before blocking");

        cache.MarkBlocked("alpha");
        Assert.AreEqual(0, catalog.GetPlugins().Count, "Plugin should be hidden after blocking");

        cache.MarkAllowed("alpha");
        Assert.AreEqual(1, catalog.GetPlugins().Count, "Plugin should be visible again after unblocking");
    }

    private sealed class StubSource : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from, DateTimeOffset to,
            Guid? calendarOwnerId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CalendarEvent>>([]);
    }
}


