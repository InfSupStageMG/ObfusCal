using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Api.Controllers;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class PeerConnectionsControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RequestPeerConnection_CreatesRequestedRecord_ForAuthenticatedConsultant()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Contoso"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var peerConnectionId = document.RootElement.GetProperty("id").GetGuid();

        Assert.AreNotEqual(Guid.Empty, peerConnectionId);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var created = await dbContext.PeerConnections.FindAsync([peerConnectionId], TestContext.CancellationToken);

        Assert.IsNotNull(created);
        Assert.AreEqual(PeerConnectionStatus.Requested, created.Status);
        Assert.AreEqual("Contoso", created.ClientOrganisationName);
        Assert.AreEqual(calendarOwnerId, created.RequestedByCalendarOwnerId);

        var listResponse = await client.GetAsync("/api/peer-connections", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode);

        var listJson = await listResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var listDocument = JsonDocument.Parse(listJson);
        Assert.AreEqual(1, listDocument.RootElement.GetArrayLength());
        Assert.AreEqual("Contoso", listDocument.RootElement[0].GetProperty("clientOrganisationName").GetString());
        Assert.AreEqual("Requested", listDocument.RootElement[0].GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task RequestPeerConnection_ReturnsConflict_WhenDuplicateForSameConsultant()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        await factory.SeedCalendarOwnerAsync(objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var firstResponse = await client.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Contoso"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("  contoso  "),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [TestMethod]
    public async Task RequestPeerConnection_ReturnsUnauthorized_WhenUnauthenticated()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Contoso"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task RequestPeerConnection_AutoProvisionsCalendarOwner_WhenMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Contoso"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await dbContext.CalendarOwners.SingleOrDefaultAsync(o => o.EntraObjectId == objectId, TestContext.CancellationToken);

        Assert.IsNotNull(owner);
        Assert.AreEqual(objectId, owner.EntraObjectId);
    }

    [TestMethod]
    public async Task ListPeerConnections_ReturnsOnlyCurrentConsultantRequests()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        var firstObjectId = Guid.NewGuid().ToString();
        await factory.SeedCalendarOwnerAsync(firstObjectId);
        using var firstClient = factory.CreateAuthenticatedClient(firstObjectId);

        var secondObjectId = Guid.NewGuid().ToString();
        await factory.SeedCalendarOwnerAsync(secondObjectId);
        using var secondClient = factory.CreateAuthenticatedClient(secondObjectId);

        await firstClient.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Contoso"),
            TestContext.CancellationToken);

        await secondClient.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Fabrikam"),
            TestContext.CancellationToken);

        var firstListResponse = await firstClient.GetAsync("/api/peer-connections", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, firstListResponse.StatusCode);

        var firstListJson = await firstListResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var firstListDocument = JsonDocument.Parse(firstListJson);
        Assert.AreEqual(1, firstListDocument.RootElement.GetArrayLength());
        Assert.AreEqual("Contoso", firstListDocument.RootElement[0].GetProperty("clientOrganisationName").GetString());
    }

    [TestMethod]
    public async Task RequestPeerConnection_ReturnsValidationProblemDetails_WhenClientOrganisationNameMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        await factory.SeedCalendarOwnerAsync(objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            "/api/peer-connections/request",
            new { },
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual("One or more validation errors occurred.", document.RootElement.GetProperty("title").GetString());
        Assert.IsTrue(document.RootElement.TryGetProperty("errors", out _));
    }
}



