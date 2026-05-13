using System.Net;
using System.Security.Claims;
using ObfusCal.Api.Authorization;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Integration.Controllers;

[TestClass]
public class EntraIdSysadminRoleTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AdminEndpoints_AcceptSysadminRoleClaim_WithoutCalendarOwnerRecord()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        var sysadminObjectId = Guid.NewGuid().ToString();
        using var adminClient = factory.CreateAuthenticatedClientWithRoles(sysadminObjectId, "Sysadmin");
        var response = await adminClient.GetAsync("/api/admin/peer-connections", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task NonSysadminUser_CannotAccessAdminEndpoints()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        var consultantObjectId = Guid.NewGuid().ToString();
        using var consultantClient = factory.CreateAuthenticatedClient(consultantObjectId);
        var response = await consultantClient.GetAsync("/api/admin/peer-connections", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task UnknownUser_CannotAccessAdminEndpoints()
    {
        await using var factory = new CustomWebApplicationFactory("Development", useTestAuthentication: true);

        var unknownObjectId = Guid.NewGuid().ToString();
        using var unknownClient = factory.CreateAuthenticatedClient(unknownObjectId);
        var response = await unknownClient.GetAsync("/api/admin/peer-connections", TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public void HasSysadminRole_ReturnsTrue_ForClaimTypesRole()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Sysadmin")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        Assert.IsTrue(AppAuthorizationPolicies.HasSysadminRole(principal));
    }

    [TestMethod]
    public void HasSysadminRole_ReturnsTrue_ForEntraRolesClaim()
    {
        var claims = new List<Claim>
        {
            new("roles", "Sysadmin")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        Assert.IsTrue(AppAuthorizationPolicies.HasSysadminRole(principal));
    }

    [TestMethod]
    public void HasSysadminRole_ReturnsFalse_ForDifferentRole()
    {
        var claims = new List<Claim>
        {
            new("roles", "CalendarOwner")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        Assert.IsFalse(AppAuthorizationPolicies.HasSysadminRole(principal));
    }
}


