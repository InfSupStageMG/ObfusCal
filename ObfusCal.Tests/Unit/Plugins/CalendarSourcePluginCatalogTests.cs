using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Domain.Models;

namespace ObfusCal.Tests.Unit.Plugins;

[TestClass]
public class CalendarSourcePluginCatalogTests
{
    // ---------------------------------------------------------------------------
    // Catalog contract
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void GetPlugins_ReturnsAllDescriptors_InAlphabeticalOrder()
    {
        var catalog = new CalendarSourcePluginCatalog([
            new CalendarSourcePluginDescriptor("zebra", "Zebra", typeof(StubSourceA), false),
            new CalendarSourcePluginDescriptor("alpha", "Alpha", typeof(StubSourceB), false),
        ]);

        var ids = catalog.GetPlugins().Select(p => p.Id).ToArray();

        CollectionAssert.AreEqual(new[] { "alpha", "zebra" }, ids);
    }

    [TestMethod]
    public void GetPlugin_FindsById_CaseInsensitively()
    {
        var catalog = new CalendarSourcePluginCatalog([
            new CalendarSourcePluginDescriptor("mock", "Mock", typeof(StubSourceA), false),
        ]);

        Assert.IsNotNull(catalog.GetPlugin("mock"));
        Assert.IsNotNull(catalog.GetPlugin("Mock"));
        Assert.IsNotNull(catalog.GetPlugin("MOCK"));
        Assert.IsNotNull(catalog.GetPlugin("  mock  "));
    }

    [TestMethod]
    public void GetPlugin_ReturnsNull_ForUnknownId()
    {
        var catalog = new CalendarSourcePluginCatalog([
            new CalendarSourcePluginDescriptor("mock", "Mock", typeof(StubSourceA), false),
        ]);

        Assert.IsNull(catalog.GetPlugin("unknown"));
    }

    [TestMethod]
    public void GetPlugin_ReturnsNull_ForNullOrWhitespace()
    {
        var catalog = new CalendarSourcePluginCatalog([
            new CalendarSourcePluginDescriptor("mock", "Mock", typeof(StubSourceA), false),
        ]);

        Assert.IsNull(catalog.GetPlugin(null!));
        Assert.IsNull(catalog.GetPlugin(""));
        Assert.IsNull(catalog.GetPlugin("   "));
    }

    // ---------------------------------------------------------------------------
    // Plugin discovery via attribute scanning
    // ---------------------------------------------------------------------------

    [TestMethod]
    public void Discover_FindsAllThreeBuiltInPlugins()
    {
        var plugins = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false);
        var ids = plugins.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(ids.Contains("graph"), "Expected 'graph' plugin");
        Assert.IsTrue(ids.Contains("ical"),  "Expected 'ical' plugin");
        Assert.IsTrue(ids.Contains("mock"),  "Expected 'mock' plugin");
    }

    [TestMethod]
    public void Discover_MarksBuiltInPlugins_AsNotExternal()
    {
        var plugins = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false);

        foreach (var plugin in plugins)
            Assert.IsFalse(plugin.IsExternalPlugin, $"Built-in plugin '{plugin.Id}' should not be marked as external");
    }

    [TestMethod]
    public void Discover_IgnoresCalendarSourceTypes_WithoutAttribute()
    {
        // StubSourceA / StubSourceB implement ICalendarSource but carry no [CalendarSourcePlugin] attribute.
        var plugins = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false);
        var types = plugins.Select(p => p.ImplementationType).ToHashSet();

        Assert.IsFalse(types.Contains(typeof(StubSourceA)), "Unannotated ICalendarSource should not appear in catalog");
        Assert.IsFalse(types.Contains(typeof(StubSourceB)), "Unannotated ICalendarSource should not appear in catalog");
    }

    [TestMethod]
    public void Discover_ReturnsDistinctPluginIds()
    {
        var plugins = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false);
        var ids = plugins.Select(p => p.Id).ToList();

        Assert.AreEqual(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "Catalog must not contain duplicate plugin IDs");
    }

    [TestMethod]
    public void Discover_ProvidesUiMetadata_ForBuiltInPlugins()
    {
        var plugins = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false);
        var graph = plugins.Single(plugin => plugin.Id == "graph");

        Assert.IsNotNull(graph.Ui);
        Assert.IsTrue(graph.Ui!.SupportsMultipleInstances);
        Assert.IsFalse(string.IsNullOrWhiteSpace(graph.Ui.ConfigurationJsonTemplate));
    }

    [TestMethod]
    public void Discover_ProvidesActionMetadata_ForPluginsWithConsentFlows()
    {
        var plugins = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false);
        var graph = plugins.Single(plugin => plugin.Id == "graph");

        var consentAction = graph.Ui?.Actions.SingleOrDefault(a => a.ActionId == "graph-instance-consent");
        Assert.IsNotNull(consentAction, "graph plugin should declare a 'graph-instance-consent' action");
        Assert.IsFalse(string.IsNullOrWhiteSpace(consentAction!.Label));
    }

    [TestMethod]
    public void Discover_DoesNotThrow_WhenSomeAssemblyTypesCannotBeLoaded()
    {
        // All currently loaded assemblies are scanned. If any assembly's GetTypes()
        // throws ReflectionTypeLoadException the catalog must still return successfully.
        IReadOnlyList<CalendarSourcePluginDescriptor>? result = null;
        Exception? thrown = null;

        try
        {
            result = CalendarSourcePluginCatalog.Discover(includeExternalPlugins: false);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.IsNull(thrown, $"Discover() must not propagate exceptions; threw: {thrown?.Message}");
        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result, "At least the built-in plugins should always be discovered");
    }

    private sealed class StubSourceA : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from, DateTimeOffset to,
            Guid? calendarOwnerId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CalendarEvent>>([]);
    }

    private sealed class StubSourceB : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from, DateTimeOffset to,
            Guid? calendarOwnerId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CalendarEvent>>([]);
    }
}
