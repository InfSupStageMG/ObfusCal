using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;
using static System.Net.HttpStatusCode;

namespace ObfusCal.Tests.Integration.Infrastructure;

[TestClass]
public class BrowserSsoTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LoginEndpoint_RedirectsToEntraAuthorizeEndpoint()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/account/login?returnUrl=%2F", TestContext.CancellationToken);

        Assert.AreEqual(Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.IsNotNull(location);
        Assert.Contains("login.microsoftonline.com", location);
        Assert.Contains("client_id=22222222-2222-2222-2222-222222222222", location);
        Assert.Contains("redirect_uri=", location);
    }

    [TestMethod]
    public async Task SwitchEndpoint_RedirectsToEntraAuthorizeEndpoint_WithSelectAccountPrompt()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/account/switch?returnUrl=%2F", TestContext.CancellationToken);

        Assert.AreEqual(Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.IsNotNull(location);
        Assert.Contains("login.microsoftonline.com", location);
        Assert.Contains("prompt=select_account", location);
    }

    [TestMethod]
    public async Task Dashboard_RendersLoginRedirect_ForAnonymousUser()
    {
        await using var factory = new CustomWebApplicationFactory("Development");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/", TestContext.CancellationToken);

        if (response.StatusCode == OK)
        {
            var html = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
            Assert.Contains("/account/login?returnUrl=%2F", html);
            return;
        }

        Assert.AreEqual(Redirect, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location);
    }

    [TestMethod]
    public async Task Dashboard_IsScopedToSignedInUsersCalendarOwner_WhenNotSysadmin()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        await factory.SeedCalendarOwnerAsync(TestAuthHandler.DefaultObjectId, name: "My Owner");
        await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString(), name: "Other Owner");
        using var client = factory.CreateAuthenticatedClient();

        var html = await client.GetStringAsync("/", TestContext.CancellationToken);

        Assert.Contains("My Owner", html);
        Assert.DoesNotContain("Other Owner", html);
    }

    [TestMethod]
    public async Task Dashboard_AutoProvisionsCalendarOwner_ForSignedInUser_WhenMissing()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        var objectId = Guid.NewGuid().ToString();
        using var client = factory.CreateAuthenticatedClient(objectId);

        var html = await client.GetStringAsync("/", TestContext.CancellationToken);

        Assert.Contains("Integration Test Calendar Owner", html);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await dbContext.CalendarOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.EntraObjectId == objectId, TestContext.CancellationToken);

        Assert.IsNotNull(owner);
        Assert.AreEqual("Integration Test Calendar Owner", owner.Name);
    }

    [TestMethod]
    public async Task CalendarOwnersPage_ShowsUnauthorizedMessage_ForNonSysadminUser()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/calendar-owners", TestContext.CancellationToken);

        if (response.StatusCode == Forbidden)
            return;

        var html = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        Assert.Contains("not authorized", html, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task CalendarOwnersPage_Loads_ForSysadminUser()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var client = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");

        var html = await client.GetStringAsync("/calendar-owners", TestContext.CancellationToken);

        Assert.Contains("Calendar Owners", html);
    }

    [TestMethod]
    public async Task PeerConnectionsPage_LoadsReadOnlyView_ForNonSysadminUser()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var client = factory.CreateAuthenticatedClient();

        var html = await client.GetStringAsync("/peers", TestContext.CancellationToken);

        Assert.Contains("My Peers", html);
        Assert.Contains("Peer onboarding is handled by administrators", html);
        Assert.DoesNotContain("Add Peer Connection", html);
    }

    [TestMethod]
    public async Task PeerConnectionsPage_LoadsAdminView_ForSysadminUser()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        using var client = factory.CreateAuthenticatedClientWithRoles(TestAuthHandler.DefaultObjectId, "Sysadmin");

        var html = await client.GetStringAsync("/peers", TestContext.CancellationToken);

        Assert.Contains("Peer Connections", html);
        Assert.Contains("Add Peer Connection", html);
        Assert.DoesNotContain("Peer onboarding is handled by administrators", html);
    }

    [TestMethod]
    public async Task CalendarOwnerDetail_DeniesAccessToDifferentOwner_ForNonSysadminUser()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);
        await factory.SeedCalendarOwnerAsync(TestAuthHandler.DefaultObjectId, name: "My Owner");
        var otherOwnerId = await factory.SeedCalendarOwnerAsync(Guid.NewGuid().ToString(), name: "Other Owner");
        using var client = factory.CreateAuthenticatedClient();

        var html = await client.GetStringAsync($"/calendar-owners/{otherOwnerId}", TestContext.CancellationToken);

        Assert.Contains("You are not authorized to view this calendar owner", html);
    }
}

