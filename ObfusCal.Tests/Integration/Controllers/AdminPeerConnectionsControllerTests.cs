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
    private const string PeerTimestampHeaderName = "X-Peer-Timestamp";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ApproveFlow_ReturnsApiKeyOnce_AndStoresOnlySha256Hash()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        var calendarOwnerObjectId = Guid.NewGuid().ToString();
        await factory.SeedCalendarOwnerAsync(calendarOwnerObjectId);

        using var calendarOwnerClient = factory.CreateAuthenticatedClient(calendarOwnerObjectId);
        var requestResponse = await calendarOwnerClient.PostAsJsonAsync(
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
            Assert.IsTrue(PeerApiKeySecurity.Verify(plaintextApiKey!, peer.ApiKeyHash));
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

        var calendarOwnerObjectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(calendarOwnerObjectId);

        using var calendarOwnerClient = factory.CreateAuthenticatedClient(calendarOwnerObjectId);
        var requestResponse = await calendarOwnerClient.PostAsJsonAsync(
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
        peerClient.DefaultRequestHeaders.Add(PeerTimestampHeaderName, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

        var pullResponse = await peerClient.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, pullResponse.StatusCode);
    }

    [TestMethod]
    public async Task RotateFlow_InvalidatesOldApiKeyAndActivatesNewApiKey()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var instanceId = "peer-rotate";
        var originalKey = "rotate-original-key";
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        var peerConnectionId = await factory.SeedPeerConnectionAsync(instanceId, originalKey);
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, calendarOwnerRef, instanceId, originalKey);

        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");
        var rotateResponse = await adminClient.PostAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/rotate-key",
            null,
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, rotateResponse.StatusCode);
        var rotateJson = await rotateResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var rotateDoc = JsonDocument.Parse(rotateJson);
        var rotatedKey = rotateDoc.RootElement.GetProperty("apiKey").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(rotatedKey));

        using var peerClient = factory.CreateClient();
        peerClient.DefaultRequestHeaders.Add(PeerTimestampHeaderName, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

        peerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", originalKey);
        var oldKeyResponse = await peerClient.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.Unauthorized, oldKeyResponse.StatusCode);

        peerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", rotatedKey);
        peerClient.DefaultRequestHeaders.Remove(PeerTimestampHeaderName);
        peerClient.DefaultRequestHeaders.Add(PeerTimestampHeaderName, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        var newKeyResponse = await peerClient.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, newKeyResponse.StatusCode);
    }

    [TestMethod]
    public async Task RevokeFlow_SetsRevokedAt_AndPeerAuthStopsImmediately()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var instanceId = "peer-revoke";
        var apiKey = "revoke-key";
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        var peerConnectionId = await factory.SeedPeerConnectionAsync(instanceId, apiKey);
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, calendarOwnerRef, instanceId, apiKey);

        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");
        var revokeResponse = await adminClient.PostAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/revoke",
            null,
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var peer = await dbContext.PeerConnections.SingleAsync(p => p.Id == peerConnectionId, TestContext.CancellationToken);
            Assert.IsNotNull(peer.RevokedAt);
        }

        using var peerClient = factory.CreateClient();
        peerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
        peerClient.DefaultRequestHeaders.Add(PeerTimestampHeaderName, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        var response = await peerClient.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        Assert.AreEqual(string.Empty, body);
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

    [TestMethod]
    public async Task Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesHttpScheme()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        var calendarOwnerObjectId = Guid.NewGuid().ToString();
        await factory.SeedCalendarOwnerAsync(calendarOwnerObjectId);
        using var calendarOwnerClient = factory.CreateAuthenticatedClient(calendarOwnerObjectId);

        var requestResponse = await calendarOwnerClient.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Contoso"),
            TestContext.CancellationToken);
        var requestJson = await requestResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var requestDocument = JsonDocument.Parse(requestJson);
        var peerConnectionId = requestDocument.RootElement.GetProperty("id").GetGuid();

        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");
        var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/approve",
            new AdminPeerConnectionsController.ApprovePeerConnectionRequest("http://peer.contoso.example"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, approveResponse.StatusCode);
    }

    [TestMethod]
    public async Task Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesPrivateIpHost()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        var calendarOwnerObjectId = Guid.NewGuid().ToString();
        await factory.SeedCalendarOwnerAsync(calendarOwnerObjectId);
        using var calendarOwnerClient = factory.CreateAuthenticatedClient(calendarOwnerObjectId);

        var requestResponse = await calendarOwnerClient.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("Contoso"),
            TestContext.CancellationToken);
        var requestJson = await requestResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var requestDocument = JsonDocument.Parse(requestJson);
        var peerConnectionId = requestDocument.RootElement.GetProperty("id").GetGuid();

        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");
        var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/approve",
            new AdminPeerConnectionsController.ApprovePeerConnectionRequest("https://10.0.0.9"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, approveResponse.StatusCode);
    }

    [TestMethod]
    public async Task RotateFlow_WritesKeyRotationAuditEvent()
    {
        var auditFilePath = Path.Join(
            Path.GetTempPath(), "ObfusCal", "tests", $"rotate-audit-{Guid.NewGuid():N}.ndjson");
        var overrides = new Dictionary<string, string?> { ["SecurityAudit:FilePath"] = auditFilePath };

        await using var factory = new CustomWebApplicationFactory(
            "Development",
            useTestAuthentication: true,
            additionalConfiguration: overrides);

        var instanceId = "peer-rotate-audit";
        var originalKey = $"rotate-audit-key-{Guid.NewGuid():N}";
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var peerConnectionId = await factory.SeedPeerConnectionAsync(instanceId, originalKey);
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, Guid.NewGuid(), instanceId, originalKey);

        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");
        var rotateResponse = await adminClient.PostAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/rotate-key",
            null,
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, rotateResponse.StatusCode);

        var events = await ReadAuditEventsAsync(auditFilePath);
        var rotationEvent = events.LastOrDefault(e =>
            string.Equals(e.EventCode, SecurityAuditEventCodes.KeyRotation, StringComparison.Ordinal));

        Assert.IsNotNull(rotationEvent, "A KEY_ROTATION audit event must be written when a peer key is rotated.");
        Assert.AreEqual(SecurityAuditOutcomes.Success, rotationEvent.Outcome);
    }

    // --- BEFORE protection: demonstrates the vulnerability each feature was introduced to fix ---

    [TestMethod]
    public async Task ApproveFlow_WithLegacySha256Hash_PeerStillAuthenticates()
    {
        // Before PBKDF2 hardening, peer API keys were stored as plain SHA256 hex hashes.
        // SHA256 is fast, non-salted, and deterministic — an attacker who obtained the hash
        // could brute-force or rainbow-table the original key.
        // This test demonstrates the legacy format is still recognised (backward compatibility
        // path in PeerApiKeySecurity.Verify) and contrasts the hash format with the secure PBKDF2 format.
        const string apiKey = "legacy-peer-key-demo";

        var legacyHash = PeerApiKeySecurity.ComputeSha256(apiKey);
        var pbkdf2Hash = PeerApiKeySecurity.Hash(apiKey);

        // The legacy hash is a short, deterministic hex string — easy to brute-force.
        Assert.IsFalse(legacyHash.StartsWith("PBKDF2$SHA256$", StringComparison.Ordinal),
            "Legacy SHA256 hash must NOT use the PBKDF2 prefix.");
        Assert.AreEqual(legacyHash, PeerApiKeySecurity.ComputeSha256(apiKey),
            "Legacy SHA256 is deterministic: the same key always produces the same hash, enabling rainbow-table attacks.");

        // The PBKDF2 hash is salted and non-deterministic.
        Assert.IsTrue(pbkdf2Hash.StartsWith("PBKDF2$SHA256$", StringComparison.Ordinal),
            "PBKDF2 hash must use the PBKDF2$SHA256$ prefix.");
        Assert.AreNotEqual(pbkdf2Hash, PeerApiKeySecurity.Hash(apiKey),
            "PBKDF2 is salted: the same key produces a different hash each time.");

        // Backward compatibility: the legacy format is still verifiable.
        Assert.IsTrue(PeerApiKeySecurity.Verify(apiKey, legacyHash),
            "Backward compatibility path must verify legacy SHA256 hashes.");
    }

    [TestMethod]
    public async Task Approve_WithSsrfValidationDisabled_AcceptsPeerHttpBaseUrl()
    {
        // Before transport security validation was added to the approve flow, any peer
        // base URL was accepted — including http:// and private-IP addresses. An admin
        // could inadvertently register a peer pointing to an internal endpoint, and the
        // outbound sync service would then make requests to that internal address.
        await using var factory = new CustomWebApplicationFactory(
            "Development",
            useTestAuthentication: true,
            disableUrlSafetyValidation: true);

        var calendarOwnerObjectId = Guid.NewGuid().ToString();
        await factory.SeedCalendarOwnerAsync(calendarOwnerObjectId);
        using var calendarOwnerClient = factory.CreateAuthenticatedClient(calendarOwnerObjectId);

        var requestResponse = await calendarOwnerClient.PostAsJsonAsync(
            "/api/peer-connections/request",
            new PeerConnectionsController.RequestPeerConnectionRequest("VulnerableContoso"),
            TestContext.CancellationToken);
        var requestJson = await requestResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var requestDocument = JsonDocument.Parse(requestJson);
        var peerConnectionId = requestDocument.RootElement.GetProperty("id").GetGuid();

        using var adminClient = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");
        var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/peer-connections/{peerConnectionId}/approve",
            new AdminPeerConnectionsController.ApprovePeerConnectionRequest("http://internal-host.corp/peer"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, approveResponse.StatusCode,
            "Without transport validation, an http:// peer base URL is accepted — demonstrating the pre-hardening vulnerability.");
    }

    private static async Task<IReadOnlyList<AuditEntry>> ReadAuditEventsAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        var lines = await File.ReadAllLinesAsync(filePath);
        var result = new List<AuditEntry>(lines.Length);
        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            result.Add(new AuditEntry(
                root.GetProperty("eventCode").GetString() ?? string.Empty,
                root.GetProperty("outcome").GetString() ?? string.Empty));
        }

        return result;
    }

    private sealed record AuditEntry(string EventCode, string Outcome);
}


