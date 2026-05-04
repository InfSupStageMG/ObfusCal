using System.Net;
using System.Net.Http.Json;
using ObfusCal.Application.Interfaces;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class StatusControllerTests
{
    private static readonly CustomWebApplicationFactory Factory = new("Development", useTestAuthentication: true);

    [TestMethod]
    public async Task GetStatus_ReturnsOk_WhenAuthenticated()
    {
        var client = Factory.CreateAuthenticatedClient();
        await Factory.SeedCalendarOwnerAsync(TestAuthHandler.DefaultObjectId);

        var response = await client.GetAsync("/api/status");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetStatus_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/status");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetStatus_ReturnsEmptyArray_WhenNoCalendarOwners()
    {
        var client = Factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/status");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<CalendarOwnerStatusEntry>>();
        Assert.IsNotNull(entries);
    }

    [TestMethod]
    public async Task GetStatus_IncludesCalendarOwnerFields()
    {
        var client = Factory.CreateAuthenticatedClient();
        var ownerId = await Factory.SeedCalendarOwnerAsync(TestAuthHandler.DefaultObjectId, name: "Status Test Owner");

        var response = await client.GetAsync("/api/status");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<CalendarOwnerStatusEntry>>();
        Assert.IsNotNull(entries);

        var entry = entries.FirstOrDefault(e => e.CalendarOwnerId == ownerId);
        Assert.IsNotNull(entry, "Expected seeded calendar owner in status response.");
        Assert.AreEqual("Status Test Owner", entry.DisplayName);
    }

    [TestMethod]
    public async Task GetStatus_IncludesPeerConnectionStatus()
    {
        var client = Factory.CreateAuthenticatedClient();
        var ownerId = await Factory.SeedCalendarOwnerAsync(TestAuthHandler.DefaultObjectId);
        await Factory.SeedCalendarOwnerPeerMappingAsync(ownerId, Guid.NewGuid());

        var response = await client.GetAsync("/api/status");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<CalendarOwnerStatusEntry>>();
        Assert.IsNotNull(entries);

        var entry = entries.FirstOrDefault(e => e.CalendarOwnerId == ownerId);
        Assert.IsNotNull(entry);
        Assert.IsTrue(entry.PeerConnections.Count > 0, "Expected at least one peer connection.");
    }

    [TestMethod]
    public async Task GetStatus_NullTimestamps_WhenNeverSynced()
    {
        var uniqueObjectId = Guid.NewGuid().ToString();
        var client = Factory.CreateAuthenticatedClient();
        var ownerId = await Factory.SeedCalendarOwnerAsync(uniqueObjectId, name: "Null Timestamp Owner");

        var response = await client.GetAsync("/api/status");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<CalendarOwnerStatusEntry>>();
        Assert.IsNotNull(entries);

        var entry = entries.FirstOrDefault(e => e.CalendarOwnerId == ownerId);
        Assert.IsNotNull(entry);
        Assert.IsNull(entry.LastSyncedAt);
        Assert.IsNull(entry.LastSyncSucceeded);
    }
}


