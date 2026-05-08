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
    private const string PeerTimestampHeaderName = "X-Peer-Timestamp";

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
        SetReplayHeader(client, DateTimeOffset.UtcNow);
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
        SetReplayHeader(client, DateTimeOffset.UtcNow);
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
        SetReplayHeader(client, DateTimeOffset.UtcNow);

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
        SetReplayHeader(client, DateTimeOffset.UtcNow);

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
        SetReplayHeader(client, DateTimeOffset.UtcNow);

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
        SetReplayHeader(client, DateTimeOffset.UtcNow);

        var response = await client.GetAsync(
            $"/api/sync/busy-slots/{Guid.NewGuid()}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PushShadowSlots_WithMissingPushScope_ReturnsForbidden()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var apiKey = $"scope-mismatch-{Guid.NewGuid():N}";
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        await factory.SeedPeerConnectionAsync(
            "peer-pull-only",
            apiKey,
            scopes: [PeerApiScopes.PullBusySlots]);
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, Guid.NewGuid(), "peer-pull-only", apiKey);
        await factory.SeedCalendarOwnerPeerMappingAsync(
            calendarOwnerId,
            Guid.NewGuid(),
            "peer-pull-only",
            apiKey,
            scopes: [PeerApiScopes.PullBusySlots]);

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) }
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        SetReplayHeader(client, DateTimeOffset.UtcNow);

        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PullBusySlotsForPeer_WithMissingPullScope_ReturnsForbidden()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var instanceId = "peer-push-only";
        var apiKey = $"scope-mismatch-{Guid.NewGuid():N}";
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        await factory.SeedPeerConnectionAsync(instanceId, apiKey, scopes: [PeerApiScopes.PushShadowSlots]);
        await factory.SeedCalendarOwnerPeerMappingAsync(
            calendarOwnerId,
            calendarOwnerRef,
            instanceId,
            apiKey,
            scopes: [PeerApiScopes.PushShadowSlots]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        SetReplayHeader(client, DateTimeOffset.UtcNow);

        var response = await client.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PushShadowSlots_WithExpiredReplayTimestamp_ReturnsUnauthorized()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, Guid.NewGuid());

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) }
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", CustomWebApplicationFactory.IntegrationTestPeerApiKey);
        SetReplayHeader(client, DateTimeOffset.UtcNow.AddMinutes(-10));

        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PushShadowSlots_WithRevokedPeer_ReturnsUnauthorized()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var instanceId = "peer-revoked";
        var apiKey = $"revoked-{Guid.NewGuid():N}";
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        await factory.SeedPeerConnectionAsync(instanceId, apiKey, revoked: true);
        await factory.SeedCalendarOwnerPeerMappingAsync(
            calendarOwnerId,
            Guid.NewGuid(),
            instanceId,
            apiKey,
            revoked: true);

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) }
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        SetReplayHeader(client, DateTimeOffset.UtcNow);

        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        Assert.AreEqual(string.Empty, body);
    }

    [TestMethod]
    public async Task PushShadowSlots_ReturnsBadRequest_WhenSlotBatchExceedsMaximum()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, Guid.NewGuid());

        var start = DateTimeOffset.UtcNow;
        var payload = Enumerable.Range(0, 501)
            .Select(i => new
            {
                start = start.AddMinutes(i),
                end = start.AddMinutes(i + 1)
            })
            .ToArray();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            CustomWebApplicationFactory.IntegrationTestPeerApiKey);
        SetReplayHeader(client, DateTimeOffset.UtcNow);

        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static void SetReplayHeader(HttpClient client, DateTimeOffset timestamp)
    {
        client.DefaultRequestHeaders.Remove(PeerTimestampHeaderName);
        client.DefaultRequestHeaders.Add(PeerTimestampHeaderName, timestamp.ToUnixTimeSeconds().ToString());
    }
}
