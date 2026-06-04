namespace ObfusCal.Application.Interfaces;

public interface ICalendarSourceAuthFlowService
{
    CalendarSourcePluginActionDescriptor? GetAuthenticationAction(IReadOnlyList<CalendarSourcePluginActionDescriptor> actions);

    Task<string> BuildAuthorizationUrlAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string actionId,
        string redirectUri,
        CancellationToken ct = default);
}

