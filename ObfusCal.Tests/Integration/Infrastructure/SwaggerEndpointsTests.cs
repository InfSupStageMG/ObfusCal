using System.Net;
using System.Text.Json;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Infrastructure;

[TestClass]
public class SwaggerEndpointsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Development_SwaggerUi_IsAvailable()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Development_OpenApiJson_IsValidJson()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(json);
        Assert.IsTrue(document.RootElement.TryGetProperty("openapi", out _));
    }

    [TestMethod]
    public async Task Development_OpenApiJson_ContainsOAuthSecurityDefinition()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(json);
        var securitySchemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("OAuth2");

        Assert.AreEqual("oauth2", securitySchemes.GetProperty("type").GetString());
        Assert.IsTrue(
            securitySchemes.GetProperty("flows").GetProperty("authorizationCode").TryGetProperty("authorizationUrl", out _));
        Assert.IsTrue(
            securitySchemes.GetProperty("flows").GetProperty("authorizationCode").TryGetProperty("tokenUrl", out _));
    }

    [TestMethod]
    public async Task Production_SwaggerEndpoints_AreNotAccessible()
    {
        await using var factory = new CustomWebApplicationFactory("Production");
        using var client = factory.CreateClient();

        var uiResponse = await client.GetAsync("/swagger/index.html", TestContext.CancellationToken);
        var jsonResponse = await client.GetAsync("/swagger/v1/swagger.json", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NotFound, uiResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, jsonResponse.StatusCode);
    }
}

