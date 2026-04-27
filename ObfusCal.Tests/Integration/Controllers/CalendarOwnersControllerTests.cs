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
            Assert.AreEqual(2, firstElement.GetPropertyCount(), "Response should only contain 'start' and 'end' fields");
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
        StringAssert.Contains(authorizationUrl, "login.microsoftonline.com");
        StringAssert.Contains(authorizationUrl, "Calendars.Read");
        StringAssert.Contains(authorizationUrl, Uri.EscapeDataString(redirectUri));
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
}



