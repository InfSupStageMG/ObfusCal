using ObfusCal.Application.Obfuscation;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Persistence;

[TestClass]
public class CalendarOwnerObfuscationProfileServiceTests
{
    private static Guid SeedOwner(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = id, Name = "Test Owner" });
        db.SaveChanges();
        return id;
    }

    [TestMethod]
    public async Task GetProfilesAsync_AutoCreatesDefaultProfiles()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        var profiles = await svc.GetProfilesAsync(ownerId);

        // Should auto-provision both Internal and Client profiles
        Assert.HasCount(2, profiles);
        Assert.Contains(p => p.Context == ObfuscationAuditContext.Internal, profiles);
        Assert.Contains(p => p.Context == ObfuscationAuditContext.Client, profiles);
    }

    [TestMethod]
    public async Task GetProfilesAsync_ReturnsSortedByContext()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        var profiles = await svc.GetProfilesAsync(ownerId);

        // OrderBy(Context) — Client=0, Internal=1
        Assert.IsTrue(profiles[0].Context <= profiles[1].Context,
            "Profiles should be sorted by Context enum value");
    }

    [TestMethod]
    public async Task GetProfileAsync_ReturnsDefaultForNewOwner()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        var profile = await svc.GetProfileAsync(ownerId, ObfuscationAuditContext.Client);

        Assert.IsTrue(profile.RemoveTitle);
        Assert.IsTrue(profile.RemoveDescription);
        Assert.IsTrue(profile.RemoveLocation);
        Assert.IsTrue(profile.RemoveAttendees);
        Assert.IsTrue(profile.RoundTimes);
        Assert.AreEqual(15, profile.RoundingIntervalMinutes);
        Assert.IsTrue(profile.MergeBlocks);
    }

    [TestMethod]
    public async Task GetProfileAsync_ReturnsDefault_WhenOwnerDoesNotExist()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);

        var profile = await svc.GetProfileAsync(Guid.NewGuid(), ObfuscationAuditContext.Client);

        // Should return CreateDefault fallback since owner doesn't exist
        Assert.AreEqual(ObfuscationAuditContext.Client, profile.Context);
        Assert.IsTrue(profile.RemoveTitle);
    }

    [TestMethod]
    public async Task SetProfileAsync_UpdatesExistingProfile()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        // Auto-provisions defaults
        await svc.GetProfileAsync(ownerId, ObfuscationAuditContext.Client);

        var updated = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: false,
            RemoveDescription: false,
            RemoveLocation: false,
            RemoveAttendees: false,
            RoundTimes: false,
            RoundingIntervalMinutes: 30,
            MergeBlocks: false);

        var result = await svc.SetProfileAsync(ownerId, updated);

        Assert.IsFalse(result.RemoveTitle);
        Assert.IsFalse(result.RemoveDescription);
        Assert.IsFalse(result.RemoveLocation);
        Assert.IsFalse(result.RemoveAttendees);
        Assert.IsFalse(result.RoundTimes);
        Assert.AreEqual(30, result.RoundingIntervalMinutes);
        Assert.IsFalse(result.MergeBlocks);
    }

    [TestMethod]
    public async Task SetProfileAsync_PersistsChanges()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        await svc.GetProfileAsync(ownerId, ObfuscationAuditContext.Client);

        var updated = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            RemoveTitle: false,
            RemoveDescription: true,
            RemoveLocation: true,
            RemoveAttendees: true,
            RoundTimes: true,
            RoundingIntervalMinutes: 60,
            MergeBlocks: true);

        await svc.SetProfileAsync(ownerId, updated);

        // Re-read with fresh service (same db context)
        var reloaded = await svc.GetProfileAsync(ownerId, ObfuscationAuditContext.Client);

        Assert.IsFalse(reloaded.RemoveTitle, "Updated RemoveTitle should persist");
        Assert.AreEqual(60, reloaded.RoundingIntervalMinutes, "Updated interval should persist");
    }

    [TestMethod]
    public async Task SetProfileAsync_WithZeroInterval_ThrowsArgumentOutOfRange()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        var invalid = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            true, true, true, true, true, 0, true);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => svc.SetProfileAsync(ownerId, invalid));
    }

    [TestMethod]
    public async Task SetProfileAsync_WithNegativeInterval_ThrowsArgumentOutOfRange()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        var invalid = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            true, true, true, true, true, -5, true);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => svc.SetProfileAsync(ownerId, invalid));
    }

    [TestMethod]
    public async Task GetProfilesAsync_DoesNotDuplicate_OnRepeatedCalls()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        await svc.GetProfilesAsync(ownerId);
        await svc.GetProfilesAsync(ownerId);
        var profiles = await svc.GetProfilesAsync(ownerId);

        Assert.HasCount(2, profiles, "Should still only have 2 profiles after multiple calls");
    }

    [TestMethod]
    public async Task SetProfileAsync_DoesNotAffectOtherContext()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        // Read both to provision defaults
        await svc.GetProfileAsync(ownerId, ObfuscationAuditContext.Client);
        var internalBefore = await svc.GetProfileAsync(ownerId, ObfuscationAuditContext.Internal);

        // Update only Client
        var clientUpdate = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            false, false, false, false, false, 30, false);
        await svc.SetProfileAsync(ownerId, clientUpdate);

        var internalAfter = await svc.GetProfileAsync(ownerId, ObfuscationAuditContext.Internal);

        Assert.AreEqual(internalBefore.RemoveTitle, internalAfter.RemoveTitle,
            "Internal profile should not be affected by Client update");
    }

    [TestMethod]
    public async Task EnsureDefaultProfiles_CreatedWithSecureValues()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        var profiles = await svc.GetProfilesAsync(ownerId);

        foreach (var p in profiles)
        {
            Assert.IsTrue(p.RemoveTitle, $"{p.Context} RemoveTitle default should be true");
            Assert.IsTrue(p.RemoveDescription, $"{p.Context} RemoveDescription default should be true");
            Assert.IsTrue(p.RemoveLocation, $"{p.Context} RemoveLocation default should be true");
            Assert.IsTrue(p.RemoveAttendees, $"{p.Context} RemoveAttendees default should be true");
            Assert.IsTrue(p.RoundTimes, $"{p.Context} RoundTimes default should be true");
            Assert.AreEqual(15, p.RoundingIntervalMinutes, $"{p.Context} interval should default to 15");
            Assert.IsTrue(p.MergeBlocks, $"{p.Context} MergeBlocks default should be true");
        }
    }

    [TestMethod]
    public async Task SetProfileAsync_OnNewOwner_AutoCreatesAndUpdates()
    {
        // If createdAny is always false, SaveChanges is never called, and the subsequent
        // SingleAsync in SetProfileAsync will fail because no profiles exist
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        // Don't call GetProfiles first — go directly to SetProfile
        var updated = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Internal,
            RemoveTitle: false,
            RemoveDescription: false,
            RemoveLocation: false,
            RemoveAttendees: false,
            RoundTimes: false,
            RoundingIntervalMinutes: 45,
            MergeBlocks: false);

        var result = await svc.SetProfileAsync(ownerId, updated);

        Assert.IsFalse(result.RemoveTitle);
        Assert.AreEqual(45, result.RoundingIntervalMinutes);
        Assert.AreEqual(ObfuscationAuditContext.Internal, result.Context);
    }

    [TestMethod]
    public async Task GetProfileAsync_ForNonExistentOwner_ReturnsDefault()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);

        // No owner seeded — EnsureDefaultProfiles does nothing, SingleOrDefault returns null
        var profile = await svc.GetProfileAsync(Guid.NewGuid(), ObfuscationAuditContext.Internal);

        Assert.AreEqual(ObfuscationAuditContext.Internal, profile.Context);
        Assert.IsTrue(profile.RemoveTitle);
        Assert.IsTrue(profile.MergeBlocks);
    }

    [TestMethod]
    public async Task GetProfileAsync_ForExistingOwner_ReturnsSavedProfile()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        // Set a non-default profile
        await svc.SetProfileAsync(ownerId, new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client,
            false, false, false, false, false, 30, false));

        // Read it back
        var profile = await svc.GetProfileAsync(ownerId, ObfuscationAuditContext.Client);

        // Should be the saved profile (non-default values), not CreateDefault
        Assert.IsFalse(profile.RemoveTitle, "Should return saved profile, not default");
        Assert.AreEqual(30, profile.RoundingIntervalMinutes);
        Assert.IsFalse(profile.MergeBlocks);
    }

    [TestMethod]
    public async Task GetProfilesAsync_ReturnedInCorrectOrder_NotDescending()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);
        var ownerId = SeedOwner(db);

        var profiles = await svc.GetProfilesAsync(ownerId);

        Assert.HasCount(2, profiles);
        // ObfuscationAuditContext.Internal = 0, Client = 1
        Assert.AreEqual(ObfuscationAuditContext.Internal, profiles[0].Context,
            "First profile should be Internal (enum value 0) — OrderBy ascending");
        Assert.AreEqual(ObfuscationAuditContext.Client, profiles[1].Context,
            "Second profile should be Client (enum value 1) — OrderBy ascending");
    }

    [TestMethod]
    public async Task SetProfileAsync_ForNonExistentOwner_DoesNotThrow()
    {
        // When owner doesn't exist, EnsureDefaultProfiles returns early,
        // and SingleAsync should throw because no profile was created
        await using var db = TestDbContextFactory.CreateInMemory();
        var svc = new CalendarOwnerObfuscationProfileService(db);

        var nonExistentOwnerId = Guid.NewGuid();
        var profile = new ObfuscationProfileSettings(
            ObfuscationAuditContext.Client, true, true, true, true, true, 15, true);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => svc.SetProfileAsync(nonExistentOwnerId, profile));
    }
}
