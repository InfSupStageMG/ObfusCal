using ObfusCal.Application.Obfuscation;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Tests.Unit.Persistence;

[TestClass]
public class ObfuscationProfileDefaultsTests
{
    [TestMethod]
    public void NewObfuscationProfile_HasSecureDefaults()
    {
        var profile = new ObfuscationProfile();

        Assert.IsTrue(profile.RemoveTitle, "RemoveTitle should default to true");
        Assert.IsTrue(profile.RemoveDescription, "RemoveDescription should default to true");
        Assert.IsTrue(profile.RemoveLocation, "RemoveLocation should default to true");
        Assert.IsTrue(profile.RemoveAttendees, "RemoveAttendees should default to true");
        Assert.IsTrue(profile.RoundTimes, "RoundTimes should default to true");
        Assert.AreEqual(15, profile.RoundingIntervalMinutes, "RoundingIntervalMinutes should default to 15");
        Assert.IsTrue(profile.MergeBlocks, "MergeBlocks should default to true");
    }

    [TestMethod]
    public void NewObfuscationProfile_PropertiesCanBeModified()
    {
        var profile = new ObfuscationProfile
        {
            RemoveTitle = false,
            RemoveDescription = false,
            RemoveLocation = false,
            RemoveAttendees = false,
            RoundTimes = false,
            RoundingIntervalMinutes = 30,
            MergeBlocks = false
        };

        Assert.IsFalse(profile.RemoveTitle);
        Assert.IsFalse(profile.RemoveDescription);
        Assert.IsFalse(profile.RemoveLocation);
        Assert.IsFalse(profile.RemoveAttendees);
        Assert.IsFalse(profile.RoundTimes);
        Assert.AreEqual(30, profile.RoundingIntervalMinutes);
        Assert.IsFalse(profile.MergeBlocks);
    }

    [TestMethod]
    public void ObfuscationProfileSettings_CreateDefault_HasSecureDefaults()
    {
        var settings = ObfuscationProfileSettings.CreateDefault(ObfuscationAuditContext.Client);

        Assert.AreEqual(ObfuscationAuditContext.Client, settings.Context);
        Assert.IsTrue(settings.RemoveTitle);
        Assert.IsTrue(settings.RemoveDescription);
        Assert.IsTrue(settings.RemoveLocation);
        Assert.IsTrue(settings.RemoveAttendees);
        Assert.IsTrue(settings.RoundTimes);
        Assert.AreEqual(15, settings.RoundingIntervalMinutes);
        Assert.IsTrue(settings.MergeBlocks);
    }

    [TestMethod]
    public void ObfuscationProfileSettings_CreateDefault_InternalContext()
    {
        var settings = ObfuscationProfileSettings.CreateDefault(ObfuscationAuditContext.Internal);

        Assert.AreEqual(ObfuscationAuditContext.Internal, settings.Context);
        Assert.IsTrue(settings.RemoveTitle);
        Assert.IsTrue(settings.RemoveDescription);
        Assert.IsTrue(settings.RemoveLocation);
        Assert.IsTrue(settings.RemoveAttendees);
        Assert.IsTrue(settings.RoundTimes);
        Assert.AreEqual(15, settings.RoundingIntervalMinutes);
        Assert.IsTrue(settings.MergeBlocks);
    }
}

