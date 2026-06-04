using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class RateLimitingTests
{
    private const string PeerTimestampHeaderName = "X-Peer-Timestamp";

    // -------------------------------------------------------------------------
    // Push shadow slots rate limiting
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task PushShadowSlots_Returns429_AfterPermitLimitExceeded()
    {
        await using var factory = new CustomWebApplicationFactory("Development",
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Sync:PushShadowSlotsRateLimitPermitLimit"] = "1",
                ["Sync:PushShadowSlotsRateLimitWindowSeconds"] = "60",
                // Keep backstop high so only the push policy fires.
                ["Sync:PeerRequestRateLimitPermitLimit"] = "1000"
            });

        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, Guid.NewGuid());

        using var client = factory.CreateClient();
        SetPeerAuthHeader(client);
        var payload = new[] { new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) } };

        var firstResponse = await client.PostAsJsonAsync("/api/shadow-slots", payload);
        Assert.AreEqual(HttpStatusCode.Created, firstResponse.StatusCode,
            "First request within the rate limit must succeed.");

        SetTimestampHeader(client);
        var secondResponse = await client.PostAsJsonAsync("/api/shadow-slots", payload);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, secondResponse.StatusCode,
            "Second request must be rejected once the push-shadow-slots rate limit is exhausted.");
    }

    [TestMethod]
    public async Task PushShadowSlots_Returns429_WithRetryAfterHeader()
    {
        await using var factory = new CustomWebApplicationFactory("Development",
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Sync:PushShadowSlotsRateLimitPermitLimit"] = "1",
                ["Sync:PushShadowSlotsRateLimitWindowSeconds"] = "60",
                ["Sync:PeerRequestRateLimitPermitLimit"] = "1000"
            });

        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, Guid.NewGuid());

        using var client = factory.CreateClient();
        SetPeerAuthHeader(client);
        var payload = new[] { new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) } };

        await client.PostAsJsonAsync("/api/shadow-slots", payload);

        SetTimestampHeader(client);
        var rateLimitedResponse = await client.PostAsJsonAsync("/api/shadow-slots", payload);

        Assert.AreEqual(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
        Assert.IsTrue(rateLimitedResponse.Headers.Contains("Retry-After"),
            "A 429 response must include a Retry-After header so clients can back off correctly.");
    }

    // -------------------------------------------------------------------------
    // Pull busy slots rate limiting
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task PullBusySlots_Returns429_AfterPermitLimitExceeded()
    {
        await using var factory = new CustomWebApplicationFactory("Development",
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Sync:PullBusySlotsRateLimitPermitLimit"] = "1",
                ["Sync:PullBusySlotsRateLimitWindowSeconds"] = "60",
                ["Sync:PeerRequestRateLimitPermitLimit"] = "1000"
            });

        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, calendarOwnerRef);

        using var client = factory.CreateClient();
        SetPeerAuthHeader(client);
        var queryUrl = $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z";

        var firstResponse = await client.GetAsync(queryUrl);
        Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode,
            "First request within the rate limit must succeed.");

        SetTimestampHeader(client);
        var secondResponse = await client.GetAsync(queryUrl);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, secondResponse.StatusCode,
            "Second request must be rejected once the pull-busy-slots rate limit is exhausted.");
    }

    [TestMethod]
    public async Task PullBusySlots_Returns429_WithRetryAfterHeader()
    {
        await using var factory = new CustomWebApplicationFactory("Development",
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Sync:PullBusySlotsRateLimitPermitLimit"] = "1",
                ["Sync:PullBusySlotsRateLimitWindowSeconds"] = "60",
                ["Sync:PeerRequestRateLimitPermitLimit"] = "1000"
            });

        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, calendarOwnerRef);

        using var client = factory.CreateClient();
        SetPeerAuthHeader(client);
        var queryUrl = $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z";

        await client.GetAsync(queryUrl);

        SetTimestampHeader(client);
        var rateLimitedResponse = await client.GetAsync(queryUrl);

        Assert.AreEqual(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
        Assert.IsTrue(rateLimitedResponse.Headers.Contains("Retry-After"),
            "A 429 response for the pull endpoint must include a Retry-After header.");
    }

    // -------------------------------------------------------------------------
    // Global API backstop rate limiting
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ApiBackstop_Returns429_WhenGlobalLimitExceeded()
    {
        // Set the global backstop to 1. Per-endpoint limits are set high so only
        // the backstop fires. Use a unique peer instance ID to avoid bucket collisions
        // with other tests in the same process.
        await using var factory = new CustomWebApplicationFactory("Development",
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Sync:PeerRequestRateLimitPermitLimit"] = "1",
                ["Sync:PeerRequestRateLimitWindowSeconds"] = "60",
                ["Sync:PushShadowSlotsRateLimitPermitLimit"] = "1000",
                ["Sync:PullBusySlotsRateLimitPermitLimit"] = "1000"
            });

        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        var calendarOwnerRef = Guid.NewGuid();
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, calendarOwnerRef);

        using var client = factory.CreateClient();
        SetPeerAuthHeader(client);

        // First request exhausts the backstop bucket.
        var firstResponse = await client.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode,
            "First request within the global backstop limit must succeed.");

        // Second request must be rejected by the backstop regardless of endpoint.
        SetTimestampHeader(client);
        var secondResponse = await client.GetAsync(
            $"/api/sync/busy-slots/{calendarOwnerRef}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.TooManyRequests, secondResponse.StatusCode,
            "Second request must be blocked by the global API backstop once its limit is exhausted.");
    }

    [TestMethod]
    public async Task RateLimited_Response_HasProblemDetailsBody()
    {
        await using var factory = new CustomWebApplicationFactory("Development",
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Sync:PushShadowSlotsRateLimitPermitLimit"] = "1",
                ["Sync:PushShadowSlotsRateLimitWindowSeconds"] = "60",
                ["Sync:PeerRequestRateLimitPermitLimit"] = "1000"
            });

        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString());
        await factory.SeedCalendarOwnerPeerMappingAsync(calendarOwnerId, Guid.NewGuid());

        using var client = factory.CreateClient();
        SetPeerAuthHeader(client);
        var payload = new[] { new { start = DateTimeOffset.UtcNow, end = DateTimeOffset.UtcNow.AddMinutes(30) } };

        await client.PostAsJsonAsync("/api/shadow-slots", payload);
        SetTimestampHeader(client);
        var rateLimitedResponse = await client.PostAsJsonAsync("/api/shadow-slots", payload);

        Assert.AreEqual(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
        Assert.AreEqual("application/problem+json", rateLimitedResponse.Content.Headers.ContentType?.MediaType,
            "Rate-limit rejection must return application/problem+json content type.");

        var body = await rateLimitedResponse.Content.ReadAsStringAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(body), "Rate-limit rejection must include a response body.");
    }

    // --- Helpers ---

    private static void SetPeerAuthHeader(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("ApiKey", CustomWebApplicationFactory.IntegrationTestPeerApiKey);
        SetTimestampHeader(client);
    }

    private static void SetTimestampHeader(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove(PeerTimestampHeaderName);
        client.DefaultRequestHeaders.Add(PeerTimestampHeaderName,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
    }
}
