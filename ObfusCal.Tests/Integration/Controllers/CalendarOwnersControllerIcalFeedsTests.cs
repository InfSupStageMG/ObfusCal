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

    private static Task<Guid> SeedAuthenticatedCalendarOwnerAsync(
        CustomWebApplicationFactory factory,
        string objectId = TestAuthHandler.DefaultObjectId) =>
        factory.SeedCalendarOwnerAsync(objectId);

    private static async Task<IReadOnlyList<CalendarOwnerICalFeed>> GetIcalFeedsAsync(
        CustomWebApplicationFactory factory,
        Guid calendarOwnerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .Where(f => f.CalendarOwnerId == calendarOwnerId)
            .OrderBy(f => f.FeedUrl)
            .ToListAsync();
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

        var owner = await factory.GetCalendarOwnerAsync(calendarOwnerId);
        Assert.AreEqual("ical", owner?.CalendarSourcePluginId,
            "Adding the first iCal feed must automatically switch the owner's calendar source to 'ical'.");
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

