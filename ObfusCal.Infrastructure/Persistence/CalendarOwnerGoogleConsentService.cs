using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class CalendarOwnerGoogleConsentService(
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    ISecretProvider secretProvider,
    GoogleOAuthDependencies googleOAuthDependencies,
    ICalendarSourceSecretProtector secretProtector,
    ICalendarSourceInstanceService calendarSourceInstanceService,
    ICalendarSourceInstanceStore calendarSourceInstanceStore)
    : ICalendarOwnerGoogleConsentService
{
    private const string GooglePluginId = "google";
    private const string GoogleSourceInstanceNotFoundMessage = "Google calendar source instance was not found.";

    private readonly IDataProtector _stateProtector = dataProtectionProvider
        .CreateProtector("ObfusCal.GoogleConsent.State.v1");

    public async Task<CalendarOwnerGoogleConsentStatus?> GetStatusAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId, GooglePluginId, ct);
        if (instance is null)
            return await dbContext.CalendarOwners
                .AsNoTracking()
                .Where(owner => owner.Id == calendarOwnerId)
                .Select(_ => new CalendarOwnerGoogleConsentStatus(false, null, null, null))
                .SingleOrDefaultAsync(ct);

        return await GetStatusAsync(calendarOwnerId, instance.Id, ct);
    }

    public async Task<CalendarOwnerGoogleConsentStatus?> GetStatusAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct);
        if (instance is null || !string.Equals(instance.PluginId, GooglePluginId, StringComparison.OrdinalIgnoreCase))
            return null;

        var secretData = ParseSecretData(instance.SecretDataJson);
        return new CalendarOwnerGoogleConsentStatus(
            !string.IsNullOrWhiteSpace(secretData?.ProtectedAccessToken)
            || !string.IsNullOrWhiteSpace(secretData?.ProtectedRefreshToken),
            secretData?.ConsentGrantedAtUtc,
            secretData?.TokenExpiresAtUtc,
            secretData?.TokenLastRefreshedAtUtc);
    }

    public async Task<bool> HasConsentAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(calendarOwnerId, ct);
        return status?.HasGoogleConsent == true;
    }

    public async Task<bool> HasConsentAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(calendarOwnerId, calendarSourceInstanceId, ct);
        return status?.HasGoogleConsent == true;
    }

    public async Task<string> BuildAuthorizationUrlAsync(Guid calendarOwnerId, string redirectUri, CancellationToken ct = default)
    {
        await EnsureOwnerExistsAsync(calendarOwnerId, ct);

        var instance = await EnsureDefaultGoogleInstanceAsync(calendarOwnerId, ct)
            ?? throw new InvalidOperationException(GoogleSourceInstanceNotFoundMessage);

        return BuildAuthorizationUrlCore(redirectUri, calendarOwnerId, instance.Id);
    }

    public async Task<string> BuildAuthorizationUrlAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string redirectUri,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct);
        if (instance is null || !string.Equals(instance.PluginId, GooglePluginId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(GoogleSourceInstanceNotFoundMessage);

        return BuildAuthorizationUrlCore(redirectUri, calendarOwnerId, calendarSourceInstanceId);
    }

    public async Task CompleteConsentAsync(
        Guid calendarOwnerId,
        string authorizationCode,
        string redirectUri,
        string state,
        CancellationToken ct = default)
    {
        await EnsureOwnerExistsAsync(calendarOwnerId, ct);

        var instance = await EnsureDefaultGoogleInstanceAsync(calendarOwnerId, ct)
            ?? throw new InvalidOperationException(GoogleSourceInstanceNotFoundMessage);

        await CompleteConsentAsync(calendarOwnerId, instance.Id, authorizationCode, redirectUri, state, ct);
    }

    public async Task CompleteConsentAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        string authorizationCode,
        string redirectUri,
        string state,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new InvalidOperationException("Authorization code is required to complete Google consent.");

        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct)
            ?? throw new InvalidOperationException(GoogleSourceInstanceNotFoundMessage);

        if (!string.Equals(instance.PluginId, GooglePluginId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The specified calendar source instance is not a Google source.");

        ValidateState(state, redirectUri, calendarOwnerId, calendarSourceInstanceId);

        var tokenResponse = await googleOAuthDependencies.TokenClient.ExchangeAuthorizationCodeAsync(authorizationCode, redirectUri, ct);
        var existingSecretData = ParseSecretData(instance.SecretDataJson);
        var secretData = new GoogleCalendarSourceCore.GoogleSourceSecretData(
            secretProtector.Protect(tokenResponse.AccessToken),
            string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
                ? existingSecretData?.ProtectedRefreshToken
                : secretProtector.Protect(tokenResponse.RefreshToken),
            DateTimeOffset.UtcNow,
            tokenResponse.ExpiresAtUtc,
            DateTimeOffset.UtcNow);

        var updated = await calendarSourceInstanceService.UpdateAsync(
            calendarOwnerId,
            calendarSourceInstanceId,
            new UpdateCalendarSourceInstanceInput(
                SecretDataJson: JsonSerializer.Serialize(secretData),
                IsEnabled: true),
            ct);

        if (updated is null)
            throw new InvalidOperationException(GoogleSourceInstanceNotFoundMessage);
    }

    public async Task CompleteConsentFromStateAsync(
        string authorizationCode,
        string redirectUri,
        string state,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new InvalidOperationException("State is required to complete Google consent.");

        GoogleConsentStatePayload payload;
        try
        {
            var json = _stateProtector.Unprotect(state);
            payload = JsonSerializer.Deserialize<GoogleConsentStatePayload>(json)
                ?? throw new InvalidOperationException("Google consent state is invalid.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Google consent state is invalid or expired.", ex);
        }

        if (payload.ExpiresAtUtc < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Google consent state has expired. Start consent again.");

        if (!string.Equals(payload.RedirectUri, redirectUri, StringComparison.Ordinal))
            throw new InvalidOperationException("Google consent redirect URI does not match the original request.");

        await CompleteConsentAsync(
            payload.CalendarOwnerId,
            payload.CalendarSourceInstanceId,
            authorizationCode,
            redirectUri,
            state,
            ct);
    }

    private string BuildAuthorizationUrlCore(string redirectUri, Guid calendarOwnerId, Guid calendarSourceInstanceId)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirect)
            || (redirect.Scheme != Uri.UriSchemeHttp && redirect.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Redirect URI must be a valid absolute http or https URI.");
        }

        var options = googleOAuthDependencies.Options.Value;
        var authorizationEndpoint = options.AuthorizationEndpoint.Trim();
        if (string.IsNullOrWhiteSpace(authorizationEndpoint))
            throw new InvalidOperationException("GoogleConsent:AuthorizationEndpoint is required.");

        var configClientId = string.IsNullOrWhiteSpace(options.ClientId) || IsPlaceholder(options.ClientId)
            ? null
            : options.ClientId;

        var clientId = configClientId
            ?? secretProvider.GetSecret(SecretKeys.GoogleConsentClientId)
            ?? throw new InvalidOperationException("GoogleConsent:ClientId is required. Set via environment variable GOOGLECONSENT__CLIENTID or configuration.");

        var scope = options.Scope.Trim();
        if (string.IsNullOrWhiteSpace(scope))
            throw new InvalidOperationException("GoogleConsent:Scope is required.");

        var state = BuildStateToken(calendarOwnerId, calendarSourceInstanceId, redirectUri);

        var query = string.Join("&",
            $"client_id={Uri.EscapeDataString(clientId)}",
            "response_type=code",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"scope={Uri.EscapeDataString(scope)}",
            "access_type=offline",
            "include_granted_scopes=true",
            "prompt=consent",
            $"state={Uri.EscapeDataString(state)}");

        return $"{authorizationEndpoint}?{query}";
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return trimmed.StartsWith('[') && trimmed.EndsWith(']');
    }

    private string BuildStateToken(Guid calendarOwnerId, Guid calendarSourceInstanceId, string redirectUri)
    {
        var payload = new GoogleConsentStatePayload(
            calendarOwnerId,
            calendarSourceInstanceId,
            redirectUri,
            DateTimeOffset.UtcNow.AddMinutes(10));

        return _stateProtector.Protect(JsonSerializer.Serialize(payload));
    }

    private void ValidateState(string state, string redirectUri, Guid calendarOwnerId, Guid calendarSourceInstanceId)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new InvalidOperationException("State is required to complete Google consent.");

        GoogleConsentStatePayload payload;
        try
        {
            var json = _stateProtector.Unprotect(state);
            payload = JsonSerializer.Deserialize<GoogleConsentStatePayload>(json)
                ?? throw new InvalidOperationException("Google consent state is invalid.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Google consent state is invalid or expired.", ex);
        }

        if (payload.ExpiresAtUtc < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Google consent state has expired. Start consent again.");

        if (!string.Equals(payload.RedirectUri, redirectUri, StringComparison.Ordinal))
            throw new InvalidOperationException("Google consent redirect URI does not match the original request.");

        if (payload.CalendarOwnerId != calendarOwnerId || payload.CalendarSourceInstanceId != calendarSourceInstanceId)
            throw new InvalidOperationException("Google consent state does not match this calendar source.");
    }

    private async Task EnsureOwnerExistsAsync(Guid calendarOwnerId, CancellationToken ct)
    {
        var exists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(owner => owner.Id == calendarOwnerId, ct);

        if (!exists)
            throw new InvalidOperationException("Calendar owner was not found.");
    }

    private async Task<CalendarSourceInstanceContext?> EnsureDefaultGoogleInstanceAsync(Guid calendarOwnerId, CancellationToken ct)
    {
        var existing = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId, GooglePluginId, ct);
        if (existing is not null)
            return existing;

        var created = await calendarSourceInstanceService.CreateAsync(
            calendarOwnerId,
            new CreateCalendarSourceInstanceInput(GooglePluginId, "Google Calendar", JsonSerializer.Serialize(new GoogleCalendarSourceCore.GoogleSourceConfiguration("primary"))),
            ct);

        return created is null
            ? null
            : await calendarSourceInstanceStore.GetAsync(calendarOwnerId, created.Id, ct);
    }

    private static GoogleCalendarSourceCore.GoogleSourceSecretData? ParseSecretData(string? secretDataJson)
    {
        if (string.IsNullOrWhiteSpace(secretDataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GoogleCalendarSourceCore.GoogleSourceSecretData>(secretDataJson);
        }
        catch
        {
            return null;
        }
    }

    private sealed record GoogleConsentStatePayload(
        Guid CalendarOwnerId,
        Guid CalendarSourceInstanceId,
        string RedirectUri,
        DateTimeOffset ExpiresAtUtc);
}

internal sealed record GoogleOAuthDependencies(
    IOptions<GoogleConsentOptions> Options,
    IGoogleOAuthTokenClient TokenClient);
