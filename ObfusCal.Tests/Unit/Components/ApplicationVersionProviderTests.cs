using ObfusCal.Api.Components.Layout;

namespace ObfusCal.Tests.Unit.Components;

[TestClass]
public class ApplicationVersionProviderTests
{
    [TestMethod]
    [DataRow("1.2.3", "1.2.3")]
    [DataRow("1.2.3-beta.4", "1.2.3-beta.4")]
    [DataRow("1.2.3+Branch.main.Sha.abcdef", "1.2.3")]
    [DataRow("v2.0.1", "2.0.1")]
    [DataRow("  v3.4.5-rc.2+sha.123456  ", "3.4.5-rc.2")]
    public void NormalizeDisplayVersion_RemovesPrefixAndMetadata(string value, string expected)
        => Assert.AreEqual(expected, ApplicationVersionProvider.NormalizeDisplayVersion(value));

    [TestMethod]
    public void NormalizeDisplayVersion_BlankValue_ReturnsDev()
        => Assert.AreEqual("dev", ApplicationVersionProvider.NormalizeDisplayVersion("  "));
}

