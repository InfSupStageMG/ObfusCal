namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerGraphConsentService
{
    Task<CalendarOwnerGraphConsentStatus?> GetStatusAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<CalendarOwnerGraphConsentStatus?> GetStatusAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default);

    Task<bool> HasConsentAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<bool> HasConsentAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default);

    string BuildAuthorizationUrl(string redirectUri);

    string BuildAuthorizationUrl(string redirectUri, GraphConsentAccessLevel accessLevel);

    Task<string> BuildAuthorizationUrlAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string redirectUri,
        GraphConsentAccessLevel accessLevel,
        CancellationToken ct = default);

    Task CompleteConsentAsync(
        Guid calendarOwnerId,
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default);

    Task CompleteConsentAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string authorizationCode,
        string redirectUri,
        CancellationToken ct = default);

    /// <summary>
    /// Completes Microsoft Graph consent by extracting the calendar owner, instance IDs, and redirect URI
    /// from the encrypted state parameter. Returns the calendar owner ID so the caller can trigger
    /// a follow-up sync. The redirect URI used at authorization start is embedded in the state token,
    /// so callers do not need to supply it.
    /// </summary>
    Task<Guid> CompleteConsentFromStateAsync(
        string authorizationCode,
        string state,
        CancellationToken ct = default);
}

public sealed record CalendarOwnerGraphConsentStatus(
    bool HasGraphConsent,
    GraphConsentAccessLevel AccessLevel,
    bool CanWriteBack,
    string? GrantedScopes,
    DateTimeOffset? GrantedAtUtc,
    DateTimeOffset? TokenExpiresAtUtc,
    DateTimeOffset? TokenLastRefreshedAtUtc);

