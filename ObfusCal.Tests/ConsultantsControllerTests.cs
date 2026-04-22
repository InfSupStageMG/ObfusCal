using System.Net;
using System.Text.Json;

namespace ObfusCal.Tests;

[TestClass]
public class ConsultantsControllerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetBusySlots_ReturnsOk_WithValidParameters()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/consultants/1/busy-slots?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.IsTrue(document.RootElement.ValueKind == JsonValueKind.Array);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenFromIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/consultants/1/busy-slots?to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenToIsMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var response = await client.GetAsync($"/api/consultants/1/busy-slots?from={from}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenFromIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "invalid-date";
        var to = "2023-01-02T00:00:00Z";
        var response = await client.GetAsync($"/api/consultants/1/busy-slots?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetBusySlots_ReturnsBadRequest_WhenToIsInvalid()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var from = "2023-01-01T00:00:00Z";
        var to = "invalid-date";
        var response = await client.GetAsync($"/api/consultants/1/busy-slots?from={from}&to={to}", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
