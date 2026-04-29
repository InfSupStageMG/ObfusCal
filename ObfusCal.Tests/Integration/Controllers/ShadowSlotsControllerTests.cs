using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Interfaces;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class ShadowSlotsControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PushShadowSlots_WithValidApiKey_StoresSlotsAndReturnsCreated()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, Guid.NewGuid());

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) },
            new { start = DateTimeOffset.UtcNow.AddHours(1), end = DateTimeOffset.UtcNow.AddHours(2) }
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            CustomWebApplicationFactory.IntegrationTestPeerApiKey);
        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IShadowSlotStore>();
        var savedSlots = await store.GetSlotsAsync(CustomWebApplicationFactory.IntegrationTestPeerInstanceId, calendarOwnerId);
        Assert.HasCount(2, savedSlots);
    }

    [TestMethod]
    public async Task PushShadowSlots_WithOwnerScopedPayload_StoresSlotsAndReturnsCreated()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, calendarOwnerRef);

        var payload = new
        {
            calendarOwnerRef,
            slots = new[]
            {
                new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) },
                new { start = DateTimeOffset.UtcNow.AddHours(1), end = DateTimeOffset.UtcNow.AddHours(2) }
            }
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            CustomWebApplicationFactory.IntegrationTestPeerApiKey);
        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IShadowSlotStore>();
        var savedSlots = await store.GetSlotsAsync(CustomWebApplicationFactory.IntegrationTestPeerInstanceId, calendarOwnerId);
        Assert.HasCount(2, savedSlots);
    }

    [TestMethod]
    public async Task PushShadowSlots_WithInvalidApiKey_ReturnsUnauthorizedAndStoresNothing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        await factory.SeedPeerConnectionAsync();

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) }
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", "invalid-peer-key");

        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IShadowSlotStore>();

        Assert.HasCount(0, await store.GetSlotsAsync(CustomWebApplicationFactory.IntegrationTestPeerInstanceId));
    }

    [TestMethod]
    public async Task PushShadowSlots_WithoutApiKeyHeader_ReturnsUnauthorized()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) }
        };

        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PushShadowSlots_WithValidApiKeyButNoOwnerMappings_ReturnsForbidden()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var instanceId = $"peer-no-mapping-{Guid.NewGuid():N}";
        var apiKey = $"test-key-{Guid.NewGuid():N}";
        await factory.SeedPeerConnectionAsync(instanceId, apiKey);

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) }
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            apiKey);

        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PullBusySlotsForPeer_WithValidApiKeyAndMapping_ReturnsOk()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, calendarOwnerRef);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            CustomWebApplicationFactory.IntegrationTestPeerApiKey);

        var from = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var to = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        var response = await client.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var json = JsonDocument.Parse(content);
        Assert.AreEqual(JsonValueKind.Array, json.RootElement.ValueKind);
    }

    [TestMethod]
    public async Task PullBusySlotsForPeer_WithoutApiKey_ReturnsUnauthorized()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/sync/busy-slots/{Guid.NewGuid()}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PullBusySlotsForPeer_WithValidApiKeyButNoMapping_ReturnsForbidden()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        await factory.SeedPeerConnectionAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            CustomWebApplicationFactory.IntegrationTestPeerApiKey);

        var response = await client.GetAsync(
            $"/api/sync/busy-slots/{Guid.NewGuid()}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
