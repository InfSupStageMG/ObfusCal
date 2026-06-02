using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Calendars;

/// <summary>
/// Resolves calendar-source authentication actions into provider-specific consent URLs.
/// </summary>
internal sealed class CalendarSourceAuthFlowService(
    ICalendarOwnerGoogleConsentService googleConsentService,
    ICalendarOwnerGraphConsentService graphConsentService) : ICalendarSourceAuthFlowService
{
    public CalendarSourcePluginActionDescriptor? GetAuthenticationAction(IReadOnlyList<CalendarSourcePluginActionDescriptor> actions)
    {
        var authenticationActions = actions
            .Where(action => CalendarSourcePluginActionIds.IsAuthenticationAction(action.ActionId))
            .ToList();

        return authenticationActions.Count == 1
            ? authenticationActions[0]
            : null;
    }

    public Task<string> BuildAuthorizationUrlAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string actionId,
        string redirectUri,
        CancellationToken ct = default)
    {
        return actionId switch
        {
            CalendarSourcePluginActionIds.GoogleInstanceConsent =>
                googleConsentService.BuildAuthorizationUrlAsync(calendarOwnerId, calendarSourceInstanceId, redirectUri, ct),
            CalendarSourcePluginActionIds.GraphInstanceConsent =>
                graphConsentService.BuildAuthorizationUrlAsync(
                    calendarOwnerId,
                    calendarSourceInstanceId,
                    redirectUri,
                    GraphConsentAccessLevel.ReadWrite,
                    ct),
            CalendarSourcePluginActionIds.GraphInstanceConsentReadOnly =>
                graphConsentService.BuildAuthorizationUrlAsync(
                    calendarOwnerId,
                    calendarSourceInstanceId,
                    redirectUri,
                    GraphConsentAccessLevel.ReadOnly,
                    ct),
            _ => throw new InvalidOperationException($"Unknown auth action: {actionId}")
        };
    }
}

