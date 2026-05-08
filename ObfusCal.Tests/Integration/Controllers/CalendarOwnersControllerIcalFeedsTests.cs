using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Api.Controllers;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class CalendarOwnersControllerIcalFeedsTests
{
    public TestContext TestContext { get; set; } = null!;

    private sealed record IcalFeedEntry(Guid Id, string FeedUrl);

    private static Task<Guid> SeedAuthenticatedCalendarOwnerAsync(
        CustomWebApplicationFactory factory,
        string objectId = TestAuthHandler.DefaultObjectId) =>
        factory.SeedCalendarOwnerAsync(objectId);

    /// <summary>
    /// Returns iCal feed entries from the CalendarSourceInstances table (new model).
    /// Legacy CalendarOwnerICalFeeds rows are also included for backward compatibility.
    /// </summary>
    private static async Task<IReadOnlyList<IcalFeedEntry>> GetIcalFeedsAsync(
        CustomWebApplicationFactory factory,
        Guid calendarOwnerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // New instance-based feeds
        var instanceRows = await dbContext.CalendarSourceInstances
            .AsNoTracking()
            .Where(i => i.CalendarOwnerId == calendarOwnerId && i.PluginId == "ical")
            .Select(i => new { i.Id, i.ConfigurationJson, i.DisplayName })
            .ToListAsync();

        var instanceFeeds = instanceRows
            .Select(i => new IcalFeedEntry(i.Id, ParseFeedUrl(i.ConfigurationJson) ?? i.DisplayName))
            .ToList();

        // Legacy feeds
        var legacyFeeds = await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .Where(f => f.CalendarOwnerId == calendarOwnerId)
            .OrderBy(f => f.FeedUrl)
            .Select(f => new IcalFeedEntry(f.Id, f.FeedUrl))
            .ToListAsync();

        return instanceFeeds.Concat(legacyFeeds).OrderBy(f => f.FeedUrl).ToList();
    }

    private static string? ParseFeedUrl(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(configurationJson);
            return doc.RootElement.TryGetProperty("FeedUrl", out var prop) ? prop.GetString() : null;
        }
        catch { return null; }
    }

    [TestMethod]
    public async Task AddIcalFeed_ReturnsCreated_AndPersistsFeed()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://calendar.example.test/feed.ics"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        var feeds = await GetIcalFeedsAsync(factory, calendarOwnerId);
        Assert.HasCount(1, feeds);
        Assert.AreEqual("https://calendar.example.test/feed.ics", feeds[0].FeedUrl);
    }

    [TestMethod]
    public async Task AddIcalFeed_SwitchesCalendarSourcePluginToIcal_WhenOwnerUsesPlaceholderProvider()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://calendar.example.test/auto-switch.ics"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        var feeds = await GetIcalFeedsAsync(factory, calendarOwnerId);
        Assert.HasCount(1, feeds,
            "Adding the first iCal feed must create exactly one iCal source instance.");
        Assert.AreEqual("https://calendar.example.test/auto-switch.ics", feeds[0].FeedUrl);
    }

    [TestMethod]
    public async Task AddIcalFeed_DoesNotChangePlugin_WhenOwnerAlreadyUsesNonMockProvider()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        // Pre-set the owner to "graph" (simulate an owner who explicitly chose Graph).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbOwner = await db.CalendarOwners.SingleAsync(o => o.Id == calendarOwnerId);
            dbOwner.CalendarSourcePluginId = "graph";
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://calendar.example.test/keep-graph.ics"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        var owner = await factory.GetCalendarOwnerAsync(calendarOwnerId);
        Assert.AreEqual("graph", owner?.CalendarSourcePluginId,
            "Explicitly chosen providers must not be overridden by adding an iCal feed.");
    }

    [TestMethod]
    public async Task AddIcalFeed_ReturnsBadRequest_WhenFeedUrlIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("not-a-url"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesHttpScheme()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("http://calendar.example.test/feed.ics"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesPrivateIpHost()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://10.0.0.5/feed.ics"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task AddIcalFeed_ReturnsValidationProblemDetails_WhenFeedUrlIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new { },
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(payload);
        Assert.AreEqual("One or more validation errors occurred.", document.RootElement.GetProperty("title").GetString());
        Assert.IsTrue(document.RootElement.TryGetProperty("errors", out _));
    }

    [TestMethod]
    public async Task AddIcalFeed_ReturnsConflict_WhenFeedAlreadyExists()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://calendar.example.test/feed.ics"),
            TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://calendar.example.test/feed.ics"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var feeds = await GetIcalFeedsAsync(factory, calendarOwnerId);
        Assert.HasCount(1, feeds);
    }

    [TestMethod]
    public async Task AddIcalFeed_ReturnsUnauthorized_WithoutToken()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://calendar.example.test/feed.ics"),
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ListIcalFeeds_ReturnsOk_WithConfiguredFeeds()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://calendar.example.test/b.ics"),
            TestContext.CancellationToken);

        await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://calendar.example.test/a.ics"),
            TestContext.CancellationToken);

        var response = await client.GetAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("https://calendar.example.test/a.ics", root[0].GetProperty("feedUrl").GetString());
        Assert.AreEqual("https://calendar.example.test/b.ics", root[1].GetProperty("feedUrl").GetString());
    }

    [TestMethod]
    public async Task DeleteIcalFeed_ReturnsNoContent_AndRemovesFeed()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds",
            new CalendarOwnersController.AddIcalFeedRequest("https://calendar.example.test/feed.ics"),
            TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

        var createdJson = await createResponse.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var createdDocument = JsonDocument.Parse(createdJson);
        var feedId = createdDocument.RootElement.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds/{feedId}",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var feeds = await GetIcalFeedsAsync(factory, calendarOwnerId);
        Assert.IsEmpty(feeds);
    }

    [TestMethod]
    public async Task DeleteIcalFeed_ReturnsNotFound_WhenFeedDoesNotExist()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        var calendarOwnerId = await SeedAuthenticatedCalendarOwnerAsync(factory, objectId);
        using var client = factory.CreateAuthenticatedClient(objectId);

        var response = await client.DeleteAsync(
            $"/api/calendar-owners/{calendarOwnerId}/ical-feeds/{Guid.NewGuid()}",
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}

