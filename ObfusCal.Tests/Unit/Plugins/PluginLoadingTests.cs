using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Application.Configuration;
using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Tests.Unit.Plugins;

[TestClass]
public class PluginLoadingTests
{
    [TestMethod]
    public void SimulatesStartup_LoadsAndDiscoversGoogleAndICloudPlugins()
    {
        // First, find where the plugins are built to
        var pluginDir = FindPluginDirectory();
        if (!Directory.Exists(pluginDir))
        {
            Assert.Fail($"Plugin directory not found at {pluginDir}");
        }

        // Load the plugins just like the app does
        LoadPluginsFromDirectory(pluginDir);

        // Now discover them
        var allowlist = new PluginAllowlistOptions
        {
            Enabled = true,
            AllowedPluginIds = ["graph", "ical", "mock", "google", "icloud"]
        };

        var discovered = CalendarSourcePluginCatalog.Discover(
            includeExternalPlugins: true,
            allowlist: allowlist,
            logger: NullLogger.Instance);

        var ids = discovered.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(ids.Contains("google"), "Expected 'google' plugin to be discovered after loading");
        Assert.IsTrue(ids.Contains("icloud"), "Expected 'icloud' plugin to be discovered after loading");

        // The exact origin can vary (dependency context vs plugins folder), but IDs must be discoverable.
        Assert.IsNotNull(discovered.FirstOrDefault(p => p.Id == "google"));
        Assert.IsNotNull(discovered.FirstOrDefault(p => p.Id == "icloud"));
    }

    private static void LoadPluginsFromDirectory(string pluginDir)
    {
        foreach (var dll in Directory.GetFiles(pluginDir, "*.dll"))
        {
            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
            }
            catch (FileLoadException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load plugin: {dll}, Error: {ex.Message}");
            }
            catch (BadImageFormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load plugin: {dll}, Error: {ex.Message}");
            }
            catch (FileNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load plugin: {dll}, Error: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load plugin: {dll}, Error: {ex.Message}");
            }
            catch (NotSupportedException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load plugin: {dll}, Error: {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load plugin: {dll}, Error: {ex.Message}");
            }
        }
    }

    private static string FindPluginDirectory()
    {
        // Try to find the plugins directory in the output
        var currentDir = AppContext.BaseDirectory;
        var pluginDir = Path.Combine(currentDir, "plugins");
        if (Directory.Exists(pluginDir))
            return pluginDir;

        // If not in the current directory, try looking in the Api output
        var apiPluginDir = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "ObfusCal.Api", "bin", "Debug", "net10.0", "plugins");
        if (Directory.Exists(apiPluginDir))
            return Path.GetFullPath(apiPluginDir);

        // Last resort, use the plugins directory relative to solution root
        var solutionRoot = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "plugins");
        return Path.GetFullPath(solutionRoot);
    }
}


