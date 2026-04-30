using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class SyncControllerTests
{
    private static readonly CustomWebApplicationFactory Factory = new("Development", useTestAuthentication: true);

    [TestMethod]
    public async Task GetPeerSyncStatus_ReturnsOk_WhenAuthenticated()
    {
        var client = Factory.CreateAuthenticatedClient();
        await Factory.SeedCalendarOwnerAsync(TestAuthHandler.DefaultObjectId);

        var response = await client.GetAsync("/api/sync/peers");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPeerSyncStatus_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/sync/peers");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PushShadowSlots_CreatedResponse_ContainsLocationHeader()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, Guid.NewGuid());

        var payload = new[]
        {
            new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) }
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            CustomWebApplicationFactory.IntegrationTestPeerApiKey);
        var response = await client.PostAsJsonAsync("/api/shadow-slots", payload);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location, "Created response should have a Location header");
        StringAssert.Contains(response.Headers.Location.ToString(), "/api/shadow-slots/");
    }

    [TestMethod]
    public async Task PullBusySlotsForPeer_ReturnsBadRequest_WhenFromMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, calendarOwnerRef);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            CustomWebApplicationFactory.IntegrationTestPeerApiKey);

        var response = await client.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?to=2023-01-02T00:00:00Z");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PullBusySlotsForPeer_ReturnsBadRequest_WhenToMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, calendarOwnerRef);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            CustomWebApplicationFactory.IntegrationTestPeerApiKey);

        var response = await client.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PushShadowSlots_ReturnsBadRequest_WithInvalidPayload()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();
        await factory.SeedPeerConnectionAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "ApiKey",
            CustomWebApplicationFactory.IntegrationTestPeerApiKey);

        // Send a plain string (not an array or object with slots)
        var content = new StringContent("\"not-an-array-or-object\"", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/shadow-slots", content);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
