using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ObfusCal.Api.Controllers;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class CalendarOwnersControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    private static Task<Guid> SeedAuthenticatedCalendarOwnerAsync(
        CustomWebApplicationFactory factory,
        string objectId = TestAuthHandler.DefaultObjectId) =>
        factory.SeedCalendarOwnerAsync(objectId);

    private static async Task<Guid> SeedConsentedCalendarOwnerAsync(
        CustomWebApplicationFactory factory,
        string objectId = TestAuthHandler.DefaultObjectId)
    {
        var calendarOwnerId = await factory.SeedCalendarOwnerAsync(objectId);
        await factory.GrantGraphConsentAsync(calendarOwnerId);
        return calendarOwnerId;
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsOk_WithValidParameters()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedConsentedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/busy-slots?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenFromIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/busy-slots?to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenToIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var from = "2023-01-01T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/busy-slots?from={from}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenFromIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var from = "invalid-date";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/busy-slots?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenToIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "invalid-date";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/busy-slots?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Tests for GetMergedFreeBusy endpoint

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsOk_WithValidParameters()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/merged-freebusy?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsBadRequest_WhenFromIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/merged-freebusy?to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsBadRequest_WhenToIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var from = "2023-01-01T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/merged-freebusy?from={from}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_BadRequest_ContainsMeaningfulMessage()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/calendar-owners/1/busy-slots", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsTrue(body.Length > 0, "BadRequest response body must not be empty");
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_BadRequest_ContainsMeaningfulMessage()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/calendar-owners/1/merged-freebusy", TestContext.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsTrue(body.Length > 0, "BadRequest response body must not be empty");
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsBadRequest_WhenFromIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var from = "invalid-date";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/merged-freebusy?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsBadRequest_WhenToIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "invalid-date";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/merged-freebusy?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsJsonWithStartAndEndFields()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/merged-freebusy?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        if (root.GetArrayLength() > 0)
        {
            var firstElement = root[0];
            Assert.IsTrue(firstElement.TryGetProperty("start", out _), "Response should contain 'start' field");
            Assert.IsTrue(firstElement.TryGetProperty("end", out _), "Response should contain 'end' field");
            Assert.IsTrue(firstElement.TryGetProperty("title", out _), "Response should contain 'title' field");
            Assert.IsTrue(firstElement.TryGetProperty("description", out _), "Response should contain 'description' field");
            Assert.IsTrue(firstElement.TryGetProperty("attendeeEmails", out _), "Response should contain 'attendeeEmails' field");
            Assert.IsTrue(firstElement.TryGetProperty("location", out _), "Response should contain 'location' field");
            Assert.AreEqual(6, firstElement.GetPropertyCount(), "Response should contain start/end plus optional metadata fields");
        }
    }


    [TestMethod]
    public async Task GetBusySlots_ReturnsUnauthorized_WithoutToken()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/calendar-owners/{calendarOwnerId}/busy-slots?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsUnauthorized_WithoutToken()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/calendar-owners/{calendarOwnerId}/merged-freebusy?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetCurrentCalendarOwner_ReturnsAuthenticatedObjectId()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/calendar-owners/me", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(TestAuthHandler.DefaultObjectId, document.RootElement.GetProperty("objectId").GetString());
    }


    [TestMethod]
    public async Task GetBusySlots_ReturnsConflict_WhenCalendarOwnerHasNotGrantedGraphConsent()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var setProviderResponse = await client.PutAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/calendar/provider",
            new CalendarOwnersController.SetCalendarProviderRequest("graph"),
            TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, setProviderResponse.StatusCode);

        var response = await client.GetAsync(
            $"/api/calendar-owners/{calendarOwnerId}/busy-slots?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual("Microsoft Graph consent required.", document.RootElement.GetProperty("title").GetString());
    }

    [TestMethod]
    public async Task GetCalendarConsentStatus_ReturnsFalseBeforeConsent_AndTrueAfterCompletion()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var beforeResponse = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/calendar/status", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, beforeResponse.StatusCode);

        var beforeJson = await beforeResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using (var beforeDocument = JsonDocument.Parse(beforeJson))
        {
            Assert.IsFalse(beforeDocument.RootElement.GetProperty("hasGraphConsent").GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, beforeDocument.RootElement.GetProperty("consentGrantedAtUtc").ValueKind);
        }

        var completeResponse = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/calendar/consent",
            new CalendarOwnersController.CompleteCalendarConsentRequest(
                FakeGraphOAuthTokenClient.ValidAuthorizationCode,
                "https://localhost/swagger/oauth2-redirect.html"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, completeResponse.StatusCode);

        var afterResponse = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/calendar/status", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, afterResponse.StatusCode);

        var afterJson = await afterResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var afterDocument = JsonDocument.Parse(afterJson);
        Assert.IsTrue(afterDocument.RootElement.GetProperty("hasGraphConsent").GetBoolean());
        Assert.AreEqual(JsonValueKind.String, afterDocument.RootElement.GetProperty("consentGrantedAtUtc").ValueKind);
        Assert.AreEqual(JsonValueKind.String, afterDocument.RootElement.GetProperty("tokenLastRefreshedAtUtc").ValueKind);
        Assert.AreEqual(JsonValueKind.String, afterDocument.RootElement.GetProperty("tokenExpiresAtUtc").ValueKind);

        var owner = await factory.GetCalendarOwnerAsync(calendarOwnerId);
        Assert.IsNotNull(owner);
        Assert.AreNotEqual(FakeGraphOAuthTokenClient.AccessToken, owner.GraphAccessTokenProtected);
        Assert.AreNotEqual(FakeGraphOAuthTokenClient.RefreshToken, owner.GraphRefreshTokenProtected);
        Assert.IsFalse(string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected));
        Assert.IsFalse(string.IsNullOrWhiteSpace(owner.GraphRefreshTokenProtected));
    }

    [TestMethod]
    public async Task GetCalendarConsentUrl_ReturnsAuthorizationUrl_ForAuthenticatedCalendarOwner()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        const string redirectUri = "https://localhost/swagger/oauth2-redirect.html";
        var response = await client.GetAsync(
            $"/api/calendar-owners/{calendarOwnerId}/calendar/consent-url?redirectUri={Uri.EscapeDataString(redirectUri)}",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var authorizationUrl = document.RootElement.GetProperty("authorizationUrl").GetString();

        Assert.IsNotNull(authorizationUrl);
        Assert.Contains("login.microsoftonline.com", authorizationUrl);
        Assert.Contains("Calendars.Read", authorizationUrl);
        Assert.Contains(Uri.EscapeDataString(redirectUri), authorizationUrl);
    }

    [TestMethod]
    public async Task CompleteCalendarConsent_ReturnsBadRequest_WhenAuthorizationCodeIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/calendar/consent",
            new CalendarOwnersController.CompleteCalendarConsentRequest(
                "expired-code",
                "https://localhost/swagger/oauth2-redirect.html"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual("Unable to complete Microsoft Graph consent.", document.RootElement.GetProperty("title").GetString());
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsForbidden_WhenAuthenticatedCalendarOwnerRequestsDifferentId()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/api/calendar-owners/{Guid.NewGuid()}/busy-slots?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsForbidden_WhenAuthenticatedCalendarOwnerRequestsDifferentId()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/api/calendar-owners/{Guid.NewGuid()}/merged-freebusy?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsNotFound_WhenAuthenticatedCalendarOwnerHasNoMatchingRecord()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var client = factory.CreateAuthenticatedClient(Guid.NewGuid().ToString());

        var response = await client.GetAsync(
            $"/api/calendar-owners/{Guid.NewGuid()}/busy-slots?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsNotFound_WhenAuthenticatedCalendarOwnerHasNoMatchingRecord()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var client = factory.CreateAuthenticatedClient(Guid.NewGuid().ToString());

        var response = await client.GetAsync(
            $"/api/calendar-owners/{Guid.NewGuid()}/merged-freebusy?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task ListObfuscationProfiles_ReturnsDefaultProfiles_ForAuthenticatedCalendarOwner()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/obfuscation-profiles", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(2, root.GetArrayLength());

        var contexts = root.EnumerateArray()
            .Select(item => item.GetProperty("context").GetString())
            .Where(context => context is not null)
            .Cast<string>()
            .ToList();

        CollectionAssert.AreEquivalent(new[] { "Internal", "Client" }, contexts);
    }

    [TestMethod]
    public async Task SetObfuscationProfile_UpdatesClientProfile_ForAuthenticatedCalendarOwner()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var updatePayload = new
        {
            removeTitle = true,
            removeDescription = false,
            removeLocation = true,
            removeAttendees = true,
            roundTimes = false,
            roundingIntervalMinutes = 30,
            mergeBlocks = false
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/obfuscation-profiles/client",
            updatePayload,
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);

        var listResponse = await client.GetAsync($"/api/calendar-owners/{calendarOwnerId}/obfuscation-profiles", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode);

        var json = await listResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var clientProfile = document.RootElement.EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("context").GetString(), "Client", StringComparison.Ordinal));

        Assert.IsFalse(clientProfile.GetProperty("removeDescription").GetBoolean());
        Assert.IsFalse(clientProfile.GetProperty("roundTimes").GetBoolean());
        Assert.AreEqual(30, clientProfile.GetProperty("roundingIntervalMinutes").GetInt32());
        Assert.IsFalse(clientProfile.GetProperty("mergeBlocks").GetBoolean());
    }

    [TestMethod]
    public async Task SetObfuscationProfile_ReturnsBadRequest_ForUnknownContext()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PutAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/obfuscation-profiles/public",
            new
            {
                removeTitle = true,
                removeDescription = true,
                removeLocation = true,
                removeAttendees = true,
                roundTimes = true,
                roundingIntervalMinutes = 15,
                mergeBlocks = true
            },
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task SetObfuscationProfile_ReturnsBadRequest_ForZeroRoundingInterval()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PutAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/obfuscation-profiles/client",
            new
            {
                removeTitle = true,
                removeDescription = true,
                removeLocation = true,
                removeAttendees = true,
                roundTimes = true,
                roundingIntervalMinutes = 0,
                mergeBlocks = true
            },
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task SetObfuscationProfile_ResponseContainsAllFields()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PutAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/obfuscation-profiles/client",
            new
            {
                removeTitle = false,
                removeDescription = false,
                removeLocation = false,
                removeAttendees = false,
                roundTimes = false,
                roundingIntervalMinutes = 60,
                mergeBlocks = false
            },
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.AreEqual("Client", root.GetProperty("context").GetString());
        Assert.IsFalse(root.GetProperty("removeTitle").GetBoolean());
        Assert.IsFalse(root.GetProperty("removeDescription").GetBoolean());
        Assert.IsFalse(root.GetProperty("removeLocation").GetBoolean());
        Assert.IsFalse(root.GetProperty("removeAttendees").GetBoolean());
        Assert.IsFalse(root.GetProperty("roundTimes").GetBoolean());
        Assert.AreEqual(60, root.GetProperty("roundingIntervalMinutes").GetInt32());
        Assert.IsFalse(root.GetProperty("mergeBlocks").GetBoolean());
    }

    [TestMethod]
    public async Task GetCalendarConsentUrl_ReturnsBadRequest_WhenRedirectUriMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.GetAsync(
            $"/api/calendar-owners/{calendarOwnerId}/calendar/consent-url",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetCalendarConsentUrl_ReturnsBadRequest_WhenRedirectUriIsRelative()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.GetAsync(
            $"/api/calendar-owners/{calendarOwnerId}/calendar/consent-url?redirectUri=/relative",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task CompleteCalendarConsent_ReturnsBadRequest_WhenAuthorizationCodeMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/calendar/consent",
            new CalendarOwnersController.CompleteCalendarConsentRequest(
                "",
                "https://localhost/callback"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task CompleteCalendarConsent_ReturnsBadRequest_WhenRedirectUriInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/calendar/consent",
            new CalendarOwnersController.CompleteCalendarConsentRequest(
                "some-code",
                "not_a_uri"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task ListObfuscationProfiles_ReturnsForbidden_ForDifferentOwner()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/api/calendar-owners/{Guid.NewGuid()}/obfuscation-profiles",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task SetObfuscationProfile_ReturnsForbidden_ForDifferentOwner()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PutAsJsonAsync(
            $"/api/calendar-owners/{Guid.NewGuid()}/obfuscation-profiles/client",
            new
            {
                removeTitle = true, removeDescription = true, removeLocation = true,
                removeAttendees = true, roundTimes = true, roundingIntervalMinutes = 15, mergeBlocks = true
            },
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

}
