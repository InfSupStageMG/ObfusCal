using ObfusCal.Api.Components;
using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Tests.Unit.Components;

[TestClass]
public class ObfuscationAuditContextDisplayTests
{
    [TestMethod]
    public void ToDisplayName_AllValues_ReturnNonEmpty()
    {
        foreach (var ctx in Enum.GetValues<ObfuscationAuditContext>())
            Assert.IsFalse(string.IsNullOrWhiteSpace(ctx.ToDisplayName()),
                $"{ctx} should have a non-empty display name");
    }

    [TestMethod]
    public void ToDisplayName_AllValues_AreDistinct()
    {
        var names = Enum.GetValues<ObfuscationAuditContext>()
            .Select(c => c.ToDisplayName())
            .ToList();

        Assert.HasCount(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase),
            "Each ObfuscationAuditContext must map to a distinct display name");
    }

    [TestMethod]
    public void ToDisplayName_Internal_ReturnsInternal()
        => Assert.AreEqual("Internal", ObfuscationAuditContext.Internal.ToDisplayName());

    [TestMethod]
    public void ToDisplayName_Client_ReturnsExternal()
        => Assert.AreEqual("External", ObfuscationAuditContext.Client.ToDisplayName());

    [TestMethod]
    public void ToDisplayHint_AllValues_ReturnNonEmpty()
    {
        foreach (var ctx in Enum.GetValues<ObfuscationAuditContext>())
            Assert.IsFalse(string.IsNullOrWhiteSpace(ctx.ToDisplayHint()),
                $"{ctx} should have a non-empty hint");
    }

    [TestMethod]
    public void ToDisplayHint_AllValues_AreDistinct()
    {
        var hints = Enum.GetValues<ObfuscationAuditContext>()
            .Select(c => c.ToDisplayHint())
            .ToList();

        Assert.HasCount(hints.Count, hints.Distinct(StringComparer.OrdinalIgnoreCase),
            "Each ObfuscationAuditContext must map to a distinct hint");
    }
}

