namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerGoogleConsentService
{
    Task<CalendarOwnerGoogleConsentStatus?> GetStatusAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<CalendarOwnerGoogleConsentStatus?> GetStatusAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default);

    Task<bool> HasConsentAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<bool> HasConsentAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default);

    Task<string> BuildAuthorizationUrlAsync(Guid calendarOwnerId, string redirectUri, CancellationToken ct = default);

    Task<string> BuildAuthorizationUrlAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, string redirectUri, CancellationToken ct = default);

    Task CompleteConsentAsync(
        Guid calendarOwnerId,
        string authorizationCode,
        string redirectUri,
        string state,
        CancellationToken ct = default);

    Task CompleteConsentAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string authorizationCode,
        string redirectUri,
        string state,
        CancellationToken ct = default);

    /// <summary>
    /// Completes Google consent by extracting the calendar owner and instance IDs from the encrypted state parameter.
    /// This is used in callback handlers where the IDs are not available in the request.
    /// </summary>
    Task CompleteConsentFromStateAsync(
        string authorizationCode,
        string redirectUri,
        string state,
        CancellationToken ct = default);
}

public sealed record CalendarOwnerGoogleConsentStatus(
    bool HasGoogleConsent,
    DateTimeOffset? GrantedAtUtc,
    DateTimeOffset? TokenExpiresAtUtc,
    DateTimeOffset? TokenLastRefreshedAtUtc);

