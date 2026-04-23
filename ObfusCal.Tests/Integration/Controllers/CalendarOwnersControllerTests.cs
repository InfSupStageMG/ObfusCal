using System.Net;
using System.Text.Json;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class CalendarOwnersControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetBusySlots_ReturnsOk_WithValidParameters()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/1/busy-slots?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenFromIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/1/busy-slots?to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenToIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/1/busy-slots?from={from}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenFromIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "invalid-date";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/1/busy-slots?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenToIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "invalid-date";
        var response = await client.GetAsync($"/api/calendar-owners/1/busy-slots?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Tests for GetMergedFreeBusy endpoint

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsOk_WithValidParameters()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/1/merged-freebusy?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsBadRequest_WhenFromIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/1/merged-freebusy?to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsBadRequest_WhenToIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/1/merged-freebusy?from={from}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsBadRequest_WhenFromIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "invalid-date";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/1/merged-freebusy?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsBadRequest_WhenToIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "invalid-date";
        var response = await client.GetAsync($"/api/calendar-owners/1/merged-freebusy?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMergedFreeBusy_ReturnsJsonWithStartAndEndFields()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/calendar-owners/1/merged-freebusy?from={from}&to={to}", TestContext.CancellationToken);

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
}



