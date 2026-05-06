using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
[DoNotParallelize]
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
    public async Task TriggerSync_ReturnsAccepted_WhenCallerHasSysadminRole()
    {
        var client = Factory.CreateAuthenticatedClientWithRoles(
            TestAuthHandler.DefaultObjectId, "Sysadmin");
        await Factory.SeedCalendarOwnerAsync(TestAuthHandler.DefaultObjectId);

        var response = await client.PostAsync("/api/sync/trigger", null);

        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
    }

    [TestMethod]
    public async Task TriggerSync_ReturnsAccepted_WhenTargetingSpecificOwner()
    {
        var client = Factory.CreateAuthenticatedClientWithRoles(
            TestAuthHandler.DefaultObjectId, "Sysadmin");
        var ownerId = await Factory.SeedCalendarOwnerAsync(TestAuthHandler.DefaultObjectId);

        var payload = new { calendarOwnerId = ownerId };
        var response = await client.PostAsJsonAsync("/api/sync/trigger", payload);

        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
    }

    [TestMethod]
    public async Task TriggerSync_ReturnsForbidden_WhenCallerLacksSysadminRole()
    {
        var client = Factory.CreateAuthenticatedClient();
        await Factory.SeedCalendarOwnerAsync(TestAuthHandler.DefaultObjectId);

        var response = await client.PostAsync("/api/sync/trigger", null);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task TriggerSync_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsync("/api/sync/trigger", null);

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
        Assert.Contains("/api/shadow-slots/", response.Headers.Location.ToString());
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
