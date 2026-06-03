using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Infrastructure.Calendars;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class AdminPluginAllowlistControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ListPlugins_ReturnsOk_ForSysadmin()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");

        var response = await adminClient.GetAsync("/api/admin/plugin-allowlist", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
            "Sysadmin must be able to list the plugin allowlist.");
    }

    [TestMethod]
    public async Task ListPlugins_ReturnsForbidden_ForNonSysadmin()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var calendarOwnerClient = factory.CreateAuthenticatedClient();

        var response = await calendarOwnerClient.GetAsync("/api/admin/plugin-allowlist", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode,
            "Non-sysadmin users must not be able to list the plugin allowlist.");
    }

    [TestMethod]
    public async Task ListPlugins_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/admin/plugin-allowlist", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode,
            "Unauthenticated requests must be rejected.");
    }

    [TestMethod]
    public async Task SetEnabled_ReturnsForbidden_ForNonSysadmin()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var calendarOwnerClient = factory.CreateAuthenticatedClient();

        var response = await calendarOwnerClient.PutAsJsonAsync(
            "/api/admin/plugin-allowlist/graph",
            new { isEnabled = false },
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task SetEnabled_DisablesPlugin_IsReflectedInSubsequentList()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");

        // Disable the "mock" plugin.
        var disableResponse = await adminClient.PutAsJsonAsync(
            "/api/admin/plugin-allowlist/mock",
            new { isEnabled = false },
            TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NoContent, disableResponse.StatusCode);

        // The listing must reflect the updated state.
        var listResponse = await adminClient.GetAsync("/api/admin/plugin-allowlist", TestContext.CancellationToken);
        var plugins = await ParsePluginListAsync(listResponse);
        var mockEntry = plugins.SingleOrDefault(p =>
            string.Equals(p.PluginId, "mock", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(mockEntry, "The 'mock' plugin entry must appear in the list after being overridden.");
        Assert.IsFalse(mockEntry.IsEnabled, "The 'mock' plugin must be listed as disabled after SetEnabled(false).");
        Assert.IsTrue(mockEntry.HasOverride, "The entry must report HasOverride=true when an explicit override exists.");
    }

    [TestMethod]
    public async Task SetEnabled_ReenablesPlugin_IsReflectedInSubsequentList()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");

        // Disable then re-enable the "mock" plugin.
        await adminClient.PutAsJsonAsync(
            "/api/admin/plugin-allowlist/mock",
            new { isEnabled = false },
            TestContext.CancellationToken);

        var enableResponse = await adminClient.PutAsJsonAsync(
            "/api/admin/plugin-allowlist/mock",
            new { isEnabled = true },
            TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NoContent, enableResponse.StatusCode);

        var listResponse = await adminClient.GetAsync("/api/admin/plugin-allowlist", TestContext.CancellationToken);
        var plugins = await ParsePluginListAsync(listResponse);
        var mockEntry = plugins.SingleOrDefault(p =>
            string.Equals(p.PluginId, "mock", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(mockEntry);
        Assert.IsTrue(mockEntry.IsEnabled, "The 'mock' plugin must be listed as enabled after SetEnabled(true).");
    }

    [TestMethod]
    public async Task RemoveOverride_ClearsOverride_PluginReturnsToDefaultEnabledState()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");

        // Disable, then remove the override.
        await adminClient.PutAsJsonAsync(
            "/api/admin/plugin-allowlist/mock",
            new { isEnabled = false },
            TestContext.CancellationToken);

        var removeResponse = await adminClient.DeleteAsync(
            "/api/admin/plugin-allowlist/mock",
            TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var listResponse = await adminClient.GetAsync("/api/admin/plugin-allowlist", TestContext.CancellationToken);
        var plugins = await ParsePluginListAsync(listResponse);
        var mockEntry = plugins.SingleOrDefault(p =>
            string.Equals(p.PluginId, "mock", StringComparison.OrdinalIgnoreCase));

        // After removing the override there should be no DB record for "mock",
        // so it either does not appear in the list at all or appears with HasOverride=false and IsEnabled=true.
        if (mockEntry is not null)
        {
            Assert.IsFalse(mockEntry.HasOverride, "After removing the override the entry must show HasOverride=false.");
            Assert.IsTrue(mockEntry.IsEnabled, "After removing the override the plugin must default to enabled.");
        }
    }

    [TestMethod]
    public async Task DisabledPlugin_IsRemovedFromInMemoryCache_Immediately()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");

        // Disable the "mock" plugin via the API.
        var disableResponse = await adminClient.PutAsJsonAsync(
            "/api/admin/plugin-allowlist/mock",
            new { isEnabled = false },
            TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NoContent, disableResponse.StatusCode);

        // Verify the in-memory PluginAllowlistCache reflects the change without a restart.
        using var scope = factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<PluginAllowlistCache>();
        Assert.Contains("mock", cache.GetBlockedPluginIds(),
            "Blocking a plugin via the admin API must update the in-memory cache immediately.");
    }

    [TestMethod]
    public async Task ReenabledPlugin_IsRemovedFromInMemoryCache_Immediately()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");

        // Disable then re-enable via the API.
        await adminClient.PutAsJsonAsync(
            "/api/admin/plugin-allowlist/mock",
            new { isEnabled = false },
            TestContext.CancellationToken);

        await adminClient.PutAsJsonAsync(
            "/api/admin/plugin-allowlist/mock",
            new { isEnabled = true },
            TestContext.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<PluginAllowlistCache>();
        Assert.DoesNotContain("mock", cache.GetBlockedPluginIds(),
            "Re-enabling a plugin via the admin API must remove it from the in-memory blocked set immediately.");
    }

    [TestMethod]
    public async Task SetEnabled_ReturnsBadRequest_WhenPluginIdIsWhitespace()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");

        // Route constraint or model validation should prevent an empty/whitespace plugin ID.
        var response = await adminClient.PutAsJsonAsync(
            "/api/admin/plugin-allowlist/%20",
            new { isEnabled = false },
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode,
            "A whitespace plugin ID must be rejected as a bad request.");
    }

    // --- Helpers ---

    private static async Task<PluginListEntry[]> ParsePluginListAsync(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray()
            .Select(element => new PluginListEntry(
                PluginId: element.GetProperty("pluginId").GetString() ?? string.Empty,
                IsEnabled: element.GetProperty("isEnabled").GetBoolean(),
                HasOverride: element.GetProperty("hasOverride").GetBoolean()))
            .ToArray();
    }

    private sealed record PluginListEntry(string PluginId, bool IsEnabled, bool HasOverride);
}
