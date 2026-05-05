using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Api.Controllers;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Security;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class AdminPeerConnectionsControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ApproveFlow_ReturnsApiKeyOnce_AndStoresOnlySha256Hash()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        var consultantObjectId = Guid.NewGuid().ToString();
        await factory.SeedCalendarOwnerAsync(consultantObjectId);

        using var consultantClient = factory.CreateAuthenticatedClient(consultantObjectId);
        var requestResponse = await consultantClient.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Contoso"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, requestResponse.StatusCode);

        var requestJson = await requestResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var requestDocument = JsonDocument.Parse(requestJson);
        var peerConnectionId = requestDocument.RootElement.GetProperty("id").GetGuid();

        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");
        var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/approve",
            new AdminPeerConnectionsController.ApprovePeerConnectionRequest("https://peer.contoso.example"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, approveResponse.StatusCode);

        var approveJson = await approveResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var approveDocument = JsonDocument.Parse(approveJson);
        var plaintextApiKey = approveDocument.RootElement.GetProperty("apiKey").GetString();

        Assert.IsFalse(string.IsNullOrWhiteSpace(plaintextApiKey));

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var peer = await dbContext.PeerConnections.SingleAsync(p => p.Id == peerConnectionId, TestContext.CancellationToken);

            Assert.AreEqual(PeerConnectionStatus.Active, peer.Status);
            Assert.AreEqual("https://peer.contoso.example", peer.BaseAddress);
            Assert.AreEqual(PeerApiKeySecurity.ComputeSha256(plaintextApiKey!), peer.ApiKeyHash);
            Assert.AreNotEqual(plaintextApiKey, peer.ApiKeyHash);
        }

        var secondApproveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/approve",
            new AdminPeerConnectionsController.ApprovePeerConnectionRequest("https://peer.contoso.example"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Conflict, secondApproveResponse.StatusCode);
    }

    [TestMethod]
    public async Task SuspendFlow_SetsStatusToSuspended_AndAuthStopsImmediately()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        var consultantObjectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(consultantObjectId);

        using var consultantClient = factory.CreateAuthenticatedClient(consultantObjectId);
        var requestResponse = await consultantClient.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Fabrikam"),
            TestContext.CancellationToken);

        var requestJson = await requestResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var requestDocument = JsonDocument.Parse(requestJson);
        var peerConnectionId = requestDocument.RootElement.GetProperty("id").GetGuid();

        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");
        var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/approve",
            new AdminPeerConnectionsController.ApprovePeerConnectionRequest("https://peer.fabrikam.example"),
            TestContext.CancellationToken);

        var approveJson = await approveResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var approveDocument = JsonDocument.Parse(approveJson);
        var plaintextApiKey = approveDocument.RootElement.GetProperty("apiKey").GetString();

        Guid calendarOwnerRef;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            calendarOwnerRef = Guid.NewGuid();
            dbContext.CalendarOwnerPeerMappings.Add(new CalendarOwnerPeerMapping
            {
                Id = Guid.NewGuid(),
                CalendarOwnerId = calendarOwnerId,
                PeerConnectionId = peerConnectionId,
                CalendarOwnerRef = calendarOwnerRef
            });
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        }

        var suspendResponse = await adminClient.PostAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/suspend",
            null,
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, suspendResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var peer = await dbContext.PeerConnections.SingleAsync(p => p.Id == peerConnectionId, TestContext.CancellationToken);
            Assert.AreEqual(PeerConnectionStatus.Suspended, peer.Status);
        }

        using var peerClient = factory.CreateClient();
        peerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", plaintextApiKey);

        var pullResponse = await peerClient.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, pullResponse.StatusCode);
    }

    [TestMethod]
    public async Task AdminEndpoints_EnforceRoleAndAuthentication()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        using var authenticatedNonAdmin = factory.CreateAuthenticatedClient();
        var forbiddenResponse = await authenticatedNonAdmin.GetAsync(
            "/api/admin/peer-connections",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        await using var unauthFactory = new CustomWebApplicationFactory("Development");
        using var anonymousClient = unauthFactory.CreateClient();
        var unauthorizedResponse = await anonymousClient.GetAsync(
            "/api/admin/peer-connections",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
    }
}


