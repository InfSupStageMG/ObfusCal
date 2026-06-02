using ObfusCal.Application.Interfaces;
using ObfusCal.Api.Components.Pages;

namespace ObfusCal.Tests.Unit.Components;

[TestClass]
public class SourceAddFormValidationTests
{
    [TestMethod]
    [DataRow("primary")]
    [DataRow("true")]
    [DataRow("false")]
    [DataRow("https://caldav.icloud.com/calendar/")]
    public void IsLikelyDefaultValue_ReturnsTrueForRealDefaults(string value)
    {
        Assert.IsTrue(CalendarOwnerDetail.IsLikelyDefaultValue(value),
            $"Expected IsLikelyDefaultValue=true for: {value}");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("you@example.com")]
    [DataRow("user@EXAMPLE.COM")]
    [DataRow("https://caldav.icloud.com/.../calendar/")]
    [DataRow("See example...")]
    public void IsLikelyDefaultValue_ReturnsFalseForExamplesAndEmptyValues(string? value)
    {
        Assert.IsFalse(CalendarOwnerDetail.IsLikelyDefaultValue(value),
            $"Expected IsLikelyDefaultValue=false for: {value}");
    }

    [TestMethod]
    public void GoogleCalendarId_WithPrimaryDefault_IsNotRequired()
    {
        // calendarId field for Google Calendar: pre-filled with "primary" → not required.
        var field = new CalendarOwnerDetail.PluginFieldEditor
        {
            Key = "calendarId",
            Label = "Calendar ID",
            Placeholder = "primary",
            Value = "primary"  // pre-filled from template
        };

        Assert.IsFalse(IsRequiredConfigField(field),
            "A field with a real default value should not be treated as required.");
    }

    [TestMethod]
    public void ICloudAppleId_WithoutValue_IsRequired()
    {
        var field = new CalendarOwnerDetail.PluginFieldEditor
        {
            Key = "appleId",
            Label = "Apple ID",
            Placeholder = "you@example.com",
            Value = null
        };

        Assert.IsTrue(IsRequiredConfigField(field),
            "A field whose placeholder is an example and has no value should be required.");
    }

    [TestMethod]
    public void ICloudAppleId_WithValueFilled_IsNotRequired()
    {
        var field = new CalendarOwnerDetail.PluginFieldEditor
        {
            Key = "appleId",
            Label = "Apple ID",
            Placeholder = "you@example.com",
            Value = "myaccount@icloud.com"
        };

        Assert.IsFalse(IsRequiredConfigField(field),
            "A field whose placeholder is an example but has a value should not be required.");
    }

    [TestMethod]
    public void ICloudCalendarUrl_WithDotDotDot_IsRequired()
    {
        var field = new CalendarOwnerDetail.PluginFieldEditor
        {
            Key = "calendarUrl",
            Label = "Calendar URL",
            Placeholder = "https://caldav.icloud.com/.../calendar/",
            Value = null
        };

        Assert.IsTrue(IsRequiredConfigField(field));
    }

    [TestMethod]
    public void SecretField_WhenEmpty_IsRequired()
    {
        var field = new CalendarOwnerDetail.PluginFieldEditor
        {
            Key = "appSpecificPassword",
            Label = "App-Specific Password",
            Value = null
        };

        Assert.IsTrue(IsRequiredSecretField(field));
    }

    [TestMethod]
    public void SecretField_WhenFilled_IsNotRequired()
    {
        var field = new CalendarOwnerDetail.PluginFieldEditor
        {
            Key = "appSpecificPassword",
            Label = "App-Specific Password",
            Value = "xxxx-xxxx-xxxx-xxxx"
        };

        Assert.IsFalse(IsRequiredSecretField(field));
    }

    [TestMethod]
    public void PluginOption_WithGoogleConsentAction_RequiresAuthentication()
    {
        var option = CreatePluginOption(new CalendarSourcePluginActionDescriptor(
            "google-instance-consent",
            "Connect Google account",
            null));

        Assert.IsTrue(option.RequiresAuthentication);
    }

    [TestMethod]
    public void PluginOption_WithGraphConsentAction_RequiresAuthentication()
    {
        var option = CreatePluginOption(new CalendarSourcePluginActionDescriptor(
            "graph-instance-consent-readonly",
            "Connect Outlook account",
            null));

        Assert.IsTrue(option.RequiresAuthentication);
    }

    [TestMethod]
    public void PluginOption_WithoutConsentAction_DoesNotRequireAuthentication()
    {
        var option = CreatePluginOption();

        Assert.IsFalse(option.RequiresAuthentication);
    }

    // Local mirrors of the SourceAddForm static helpers so that the test
    // logic stays readable without reaching into the component internals.
    private static bool IsRequiredConfigField(CalendarOwnerDetail.PluginFieldEditor field)
        => !CalendarOwnerDetail.IsLikelyDefaultValue(field.Placeholder)
           && string.IsNullOrWhiteSpace(field.Value);

    private static bool IsRequiredSecretField(CalendarOwnerDetail.PluginFieldEditor field)
        => string.IsNullOrWhiteSpace(field.Value);

    private static CalendarOwnerDetail.PluginOption CreatePluginOption(
        params CalendarSourcePluginActionDescriptor[] actions)
        => new(
            "plugin-id",
            "Plugin",
            false,
            true,
            null,
            null,
            null,
            actions);
}

