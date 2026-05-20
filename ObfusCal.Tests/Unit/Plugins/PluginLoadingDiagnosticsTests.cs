using System.Runtime.Loader;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Tests.Unit.Plugins;

/// <summary>
/// Integration tests to verify that Google and iCloud plugins are properly loaded
/// and discoverable at runtime.
/// </summary>
[TestClass]
public class GoogleICloudPluginIntegrationTests
{
    /// <summary>
    /// Verifies that the plugins/ folder exists in the test output directory.
    /// </summary>
    [TestMethod]
    public void PluginFolder_ShouldExist()
    {
        var pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");
        Assert.IsTrue(Directory.Exists(pluginFolder), $"Plugin folder should exist at: {pluginFolder}");
    }

    /// <summary>
    /// Verifies that the Google and iCloud plugin DLLs exist in the plugins/ folder.
    /// </summary>
    [TestMethod]
    public void GoogleAndICloudDlls_ShouldExistInPluginFolder()
    {
        var pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");
        var googleDll = Path.Combine(pluginFolder, "ObfusCal.Plugins.GoogleCalendar.dll");
        var icloudDll = Path.Combine(pluginFolder, "ObfusCal.Plugins.ICloudCalendar.dll");

        Assert.IsTrue(File.Exists(googleDll), $"Google plugin DLL not found at: {googleDll}");
        Assert.IsTrue(File.Exists(icloudDll), $"iCloud plugin DLL not found at: {icloudDll}");
    }

    /// <summary>
    /// Verifies that when plugin assemblies are explicitly loaded (as they should be during app startup),
    /// Google and iCloud plugins are discovered properly.
    /// </summary>
    [TestMethod]
    public void GoogleICloudPlugins_ShouldBeDiscovered_WhenAssembliesAreLoaded()
    {
        // This test verifies the behavior that should occur during app startup:
        // 1. DependencyInjection.LoadPluginAssemblies() loads all DLLs from plugins/ folder
        // 2. CalendarSourcePluginCatalog.Discover() discovers them

        var pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");

        // Manually load the plugin DLLs (simulating what LoadPluginAssemblies() does at startup)
        foreach (var dll in Directory.GetFiles(pluginFolder, "*.dll"))
        {
            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to load {Path.GetFileName(dll)}: {ex.Message}");
            }
        }

        // Now discover plugins with the proper allowlist
        var allowlist = new Application.Configuration.PluginAllowlistOptions
        {
            Enabled = true,
            AllowedPluginIds = ["graph", "ical", "mock", "google", "icloud"]
        };

        var discovered = CalendarSourcePluginCatalog.Discover(
            includeExternalPlugins: true,
            allowlist: allowlist);

        var ids = discovered.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(ids.Contains("google"), "Expected 'google' plugin to be discovered");
        Assert.IsTrue(ids.Contains("icloud"), "Expected 'icloud' plugin to be discovered");

        // The exact origin can vary (dependency context vs plugins folder), but IDs must be discoverable.
        Assert.IsNotNull(discovered.FirstOrDefault(p => p.Id == "google"));
        Assert.IsNotNull(discovered.FirstOrDefault(p => p.Id == "icloud"));
    }
}





