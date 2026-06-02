using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Calendars;

internal static class GraphConsentAccessPolicy
{
    public static bool AllowsOwnerWriteBack(string? grantedScopes)
        => string.IsNullOrWhiteSpace(grantedScopes)
           || AllowsWriteBackByScope(grantedScopes);

    public static bool AllowsInstanceWriteBack(GraphCalendarSource.GraphSourceSecretData? secretData)
    {
        if (secretData is null)
            return false;

        return secretData.RequestedAccessLevel != GraphConsentAccessLevel.ReadOnly && AllowsWriteBackByScope(secretData.GrantedScopes);
    }

    public static GraphConsentAccessLevel ResolveInstanceAccessLevel(GraphCalendarSource.GraphSourceSecretData? secretData)
        => AllowsInstanceWriteBack(secretData)
            ? GraphConsentAccessLevel.ReadWrite
            : GraphConsentAccessLevel.ReadOnly;

    public static bool AllowsWriteBackByScope(string? grantedScopes)
        => !string.IsNullOrWhiteSpace(grantedScopes)
           && grantedScopes.Contains("Calendars.ReadWrite", StringComparison.OrdinalIgnoreCase);
}

