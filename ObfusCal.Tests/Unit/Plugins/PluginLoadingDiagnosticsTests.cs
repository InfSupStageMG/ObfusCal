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
    [TestMethod]
    public void PluginFolder_ShouldExist()
    {
        var pluginFolder = Path.Join(AppContext.BaseDirectory, "plugins");
        Assert.IsTrue(Directory.Exists(pluginFolder), $"Plugin folder should exist at: {pluginFolder}");
    }

    [TestMethod]
    public void GoogleAndICloudDlls_ShouldExistInPluginFolder()
    {
        var pluginFolder = Path.Join(AppContext.BaseDirectory, "plugins");
        var googleDll = Path.Join(pluginFolder, "ObfusCal.Plugins.GoogleCalendar.dll");
        var icloudDll = Path.Join(pluginFolder, "ObfusCal.Plugins.ICloudCalendar.dll");

        Assert.IsTrue(File.Exists(googleDll), $"Google plugin DLL not found at: {googleDll}");
        Assert.IsTrue(File.Exists(icloudDll), $"iCloud plugin DLL not found at: {icloudDll}");
    }

    [TestMethod]
    public void GoogleICloudPlugins_ShouldBeDiscovered_WhenAssembliesAreLoaded()
    {

        var pluginFolder = Path.Join(AppContext.BaseDirectory, "plugins");

        // Manually load the plugin DLLs (simulating what LoadPluginAssemblies() does at startup)
        foreach (var dll in Directory.GetFiles(pluginFolder, "*.dll"))
        {
            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
            }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
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





