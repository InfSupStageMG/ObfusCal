using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Persistence;

[TestClass]
public class CalendarOwnerICloudConfigurationServiceTests
{
    [TestMethod]
    public async Task GetConfigurationAsync_WithUnconfiguredIcloudInstance_ReturnsConfiguredFalse()
    {
        var (service, db, instances) = Setup();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Unconfigured" });
        await db.SaveChangesAsync();
        await instances.CreateAsync(ownerId, new CreateCalendarSourceInstanceInput("icloud", "Personal iCloud"));

        var result = await service.GetConfigurationAsync(ownerId);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsConfigured);
    }

    [TestMethod]
    public async Task GetConfigurationAsync_WithUnknownOwner_ReturnsNull()
    {
        var (service, _, _) = Setup();

        var result = await service.GetConfigurationAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SetConfigurationAsync_CreatesAndStoresIcloudInstanceConfiguration()
    {
        var (service, db, instances) = Setup();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Test Owner" });
        await db.SaveChangesAsync();

        var input = new CalendarOwnerICloudConfigurationInput(
            "https://caldav.icloud.com/123456789/calendar/",
            "user@example.com",
            "abcd-efgh-ijkl-mnop");

        var result = await service.SetConfigurationAsync(ownerId, input);
        var persisted = await instances.GetFirstAsync(ownerId, "icloud");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsConfigured);
        Assert.AreEqual(input.CalendarUrl, result.CalendarUrl);
        Assert.IsNotNull(persisted);
        Assert.IsTrue(persisted.IsEnabled);
        Assert.IsTrue(persisted.ConfigurationJson?.Contains("caldav.icloud.com", StringComparison.Ordinal) == true);
        Assert.IsTrue(persisted.SecretDataJson?.Contains("user@example.com", StringComparison.Ordinal) == true);
    }

    [TestMethod]
    public async Task SetConfigurationAsync_WithUnknownOwner_ReturnsNull()
    {
        var (service, _, _) = Setup();

        var result = await service.SetConfigurationAsync(
            Guid.NewGuid(),
            new CalendarOwnerICloudConfigurationInput(
                "https://caldav.icloud.com/123456789/calendar/",
                "user@example.com",
                "password"));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ClearConfigurationAsync_DisablesAndClearsInstancePayload()
    {
        var (service, db, instances) = Setup();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Test Owner" });
        await db.SaveChangesAsync();

        var created = await instances.CreateAsync(ownerId, new CreateCalendarSourceInstanceInput("icloud", "Work iCloud"));
        Assert.IsNotNull(created);
        await service.SetConfigurationAsync(ownerId, created.Id, new CalendarOwnerICloudConfigurationInput(
            "https://caldav.icloud.com/123456789/calendar/",
            "user@example.com",
            "password"));

        var cleared = await service.ClearConfigurationAsync(ownerId, created.Id);
        var persisted = await instances.GetAsync(ownerId, created.Id);

        Assert.IsTrue(cleared);
        Assert.IsNotNull(persisted);
        Assert.IsFalse(persisted.IsEnabled);
        Assert.AreEqual(string.Empty, persisted.ConfigurationJson);
        Assert.AreEqual(string.Empty, persisted.SecretDataJson);
    }

    [TestMethod]
    public async Task GetConfigurationAsync_MasksStoredAppleId()
    {
        var (service, db, _) = Setup();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Test Owner" });
        await db.SaveChangesAsync();

        await service.SetConfigurationAsync(ownerId, new CalendarOwnerICloudConfigurationInput(
            "https://caldav.icloud.com/123456789/calendar/",
            "firstname.lastname@icloud.com",
            "password"));

        var result = await service.GetConfigurationAsync(ownerId);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsConfigured);
        Assert.IsNotNull(result.AppleIdHint);
        Assert.DoesNotContain("firstname.lastname", result.AppleIdHint);
        Assert.Contains("*", result.AppleIdHint);
        Assert.Contains("icloud.com", result.AppleIdHint);
    }

    private static (ICalendarOwnerICloudConfigurationService Service, AppDbContext DbContext, FakeCalendarSourceInstanceService Instances) Setup()
    {
        var dbContext = TestDbContextFactory.CreateInMemory();
        var instances = new FakeCalendarSourceInstanceService(ownerId => dbContext.CalendarOwners.Any(owner => owner.Id == ownerId));
        var service = new CalendarOwnerICloudConfigurationService(
            instances,
            instances);

        return (service, dbContext, instances);
    }

    private sealed class PassthroughSecretProtector : ICalendarSourceSecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string protectedValue) => protectedValue;
    }
}
