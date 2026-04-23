using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Core.Interfaces;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class ShadowSlotsControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PushShadowSlots_WithValidPeerHeader_StoresSlotsAndReturnsCreated()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) },
            new { start = DateTimeOffset.UtcNow.AddHours(1), end = DateTimeOffset.UtcNow.AddHours(2) }
        };

        client.DefaultRequestHeaders.Add("X-Peer-Id", "peer-a");
        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IShadowSlotStore>();
        var savedSlots = await store.GetSlotsAsync("peer-a");
        Assert.HasCount(2, savedSlots);
    }

    [TestMethod]
    public async Task PushShadowSlots_WithUnknownPeerHeader_ReturnsUnauthorizedAndStoresNothing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) }
        };

        client.DefaultRequestHeaders.Add("X-Peer-Id", "peer-unknown");

        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IShadowSlotStore>();

        Assert.HasCount(0, await store.GetSlotsAsync("peer-a"));
        Assert.HasCount(0, await store.GetSlotsAsync("peer-unknown"));
    }

    [TestMethod]
    public async Task PushShadowSlots_WithoutPeerHeader_ReturnsBadRequest()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) }
        };

        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
