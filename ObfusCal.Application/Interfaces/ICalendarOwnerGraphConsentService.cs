namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerGraphConsentService
{
    Task<CalendarOwnerGraphConsentStatus?> GetStatusAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<CalendarOwnerGraphConsentStatus?> GetStatusAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default);

    Task<bool> HasConsentAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<bool> HasConsentAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default);

    string BuildAuthorizationUrl(string redirectUri);

    Task<string> BuildAuthorizationUrlAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, string redirectUri, CancellationToken ct = default);

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
}

public sealed record CalendarOwnerGraphConsentStatus(
    bool HasGraphConsent,
    DateTimeOffset? GrantedAtUtc,
    DateTimeOffset? TokenExpiresAtUtc,
    DateTimeOffset? TokenLastRefreshedAtUtc);

