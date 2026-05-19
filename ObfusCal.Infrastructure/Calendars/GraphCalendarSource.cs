using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using CalendarEvent = ObfusCal.Domain.Models.CalendarEvent;

namespace ObfusCal.Infrastructure.Calendars;

[CalendarSourcePlugin("graph", "Microsoft Graph")]
[CalendarSourcePluginUi(
    supportsMultipleInstances: true,
    configurationJsonTemplate: "{\"calendarId\":\"primary\"}",
    setupHint: "Use the Graph consent flow to populate tokens for each source instance.")]
[CalendarSourcePluginAction(
    "graph-instance-consent-readonly",
    "Connect Microsoft (read-only)",
    hint: "Authorizes ObfusCal to read your Microsoft Graph calendar for this source instance without write-back permissions.")]
[CalendarSourcePluginAction(
    "graph-instance-consent",
    "Connect Microsoft (write-back)",
    hint: "Authorizes ObfusCal to read your Microsoft Graph calendar and maintain ObfusCal-managed busy placeholders for this source instance.")]
public sealed partial class GraphCalendarSource(
    HttpClient httpClient,
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    IGraphOAuthTokenClient tokenClient,
    ICalendarSourceInstanceStore calendarSourceInstanceStore,
    ILogger<GraphCalendarSource> logger)
    : ICalendarSource, ICalendarWriteBack, ICalendarSourceInstanceWriteBack, ICalendarSourceReadinessEvaluator, ICalendarSourceInstanceHandler, ICalendarSourceInstanceReadinessEvaluator
{
    private const string GraphCalendarViewPath = "v1.0/me/calendarView";
    private const string GraphEventsPath = "v1.0/me/events";
    private const int GraphCalendarViewPageSize = 1000;
    private const string ObfusCalPropertyNamespace = "e65f4da1-6bc9-45ac-a364-5b91d9b5f3e0";
    private const string ManagedPropertyId = "String {" + ObfusCalPropertyNamespace + "} Name ObfusCal.Managed";
    private const string SlotIdPropertyId  = "String {" + ObfusCalPropertyNamespace + "} Name ObfusCal.SlotId";

    private readonly HttpClient _httpClient = httpClient;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IGraphOAuthTokenClient _tokenClient = tokenClient;
    private readonly ICalendarSourceInstanceStore _calendarSourceInstanceStore = calendarSourceInstanceStore;
    private readonly ILogger<GraphCalendarSource> _logger = logger;

    private readonly IDataProtector _tokenProtector = dataProtectionProvider
        .CreateProtector("ObfusCal.GraphConsent.TokenStore.v1");

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? calendarOwnerId = null,
        CancellationToken ct = default)
    {
        if (from > to)
            throw new ArgumentException("The start of the query window must be before the end.", nameof(from));

        ct.ThrowIfCancellationRequested();

        if (calendarOwnerId is null)
            return [];

        var owner = await _dbContext.CalendarOwners
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId.Value, ct);

        if (owner is null)
            return [];

        var tokenSession = await CreateOwnerTokenSessionAsync(owner, ct);
        if (tokenSession is null)
            return [];

        using var response = await GetCalendarViewWithRetryAsync(tokenSession, from, to, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Graph calendar fetch failed for calendar owner {CalendarOwnerId} with HTTP {StatusCode}.",
                calendarOwnerId.Value,
                (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphCalendarViewResponse>(cancellationToken: ct);
        var events = await CollectAllPagesAsync(payload, tokenSession, ct);

        return events
            .Where(source => !IsManagedEvent(source))
            .Select(MapEvent)
            .Where(mapped => mapped is not null)
            .Select(mapped => mapped!)
            .Where(e => e.Start < to && e.End > from)
            .OrderBy(e => e.Start)
            .ToList();
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarSourceInstanceContext instance,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (from > to)
            throw new ArgumentException("The start of the query window must be before the end.", nameof(from));

        ct.ThrowIfCancellationRequested();

        var tokenSession = await CreateInstanceTokenSessionAsync(instance, ct);
        if (tokenSession is null)
            return [];

        using var response = await GetCalendarViewWithRetryAsync(tokenSession, from, to, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Graph calendar fetch failed for calendar source instance {CalendarSourceInstanceId} with HTTP {StatusCode}.",
                instance.Id,
                (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphCalendarViewResponse>(cancellationToken: ct);
        var events = await CollectAllPagesAsync(payload, tokenSession, ct);

        return events
            .Where(source => !IsManagedEvent(source))
            .Select(MapEvent)
            .Where(mapped => mapped is not null)
            .Select(mapped => mapped!)
            .Where(e => e.Start < to && e.End > from)
            .OrderBy(e => e.Start)
            .ToList();
    }

    public async Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var owner = await _dbContext.CalendarOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId, ct);

        if (owner is null)
            return CalendarSourceReadiness.NotReady("Calendar owner not found.");

        var hasConsent = !string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected)
            || !string.IsNullOrWhiteSpace(owner.GraphRefreshTokenProtected);

        if (!hasConsent)
        {
            return CalendarSourceReadiness.NotReady(
                "Microsoft Graph consent required.",
                "This calendar owner has not granted Microsoft Graph calendar consent yet. Complete consent before requesting busy slots.");
        }

        var canWriteBack = AllowsWriteBack(owner.GraphGrantedScopes);
        return hasConsent
            ? CalendarSourceReadiness.Ready(
                canWriteBack ? "Connected (write-back enabled)." : "Connected (read-only).",
                canWriteBack
                    ? "Microsoft Graph consent includes calendar write permissions."
                    : "Microsoft Graph consent is read-only; write-back placeholders are disabled for this connection.")
            : CalendarSourceReadiness.NotReady("Microsoft Graph consent required.");
    }

    public Task<CalendarSourceReadiness> GetReadinessAsync(CalendarSourceInstanceContext instance, CancellationToken ct = default)
    {
        var secretData = ParseSecretData(instance.SecretDataJson);
        var hasConsent = !string.IsNullOrWhiteSpace(secretData?.ProtectedAccessToken)
            || !string.IsNullOrWhiteSpace(secretData?.ProtectedRefreshToken);

        if (!hasConsent)
        {
            return Task.FromResult(CalendarSourceReadiness.NotReady(
                "Microsoft Graph consent required.",
                "Complete Microsoft Graph consent for this source instance before requesting busy slots."));
        }

        var canWriteBack = AllowsWriteBack(secretData?.GrantedScopes);

        return Task.FromResult(hasConsent
            ? CalendarSourceReadiness.Ready(
                canWriteBack ? "Connected (write-back enabled)." : "Connected (read-only).",
                canWriteBack
                    ? "Microsoft Graph consent includes calendar write permissions."
                    : "Microsoft Graph consent is read-only; write-back placeholders are disabled for this source instance.")
            : CalendarSourceReadiness.NotReady("Microsoft Graph consent required."));
    }


    private async Task<string?> GetOrRefreshOwnerAccessTokenAsync(CalendarOwner owner, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected))
            return null;

        string accessToken;
        try
        {
            accessToken = _tokenProtector.Unprotect(owner.GraphAccessTokenProtected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Unable to read Graph access token for calendar owner {CalendarOwnerId}.",
                owner.Id);
            return null;
        }

        var refreshed = await RefreshIfExpiringAsync(owner, accessToken, ct);
        return string.IsNullOrWhiteSpace(refreshed) ? null : refreshed;
    }

    private async Task<GraphAccessTokenSession?> CreateOwnerTokenSessionAsync(CalendarOwner owner, CancellationToken ct)
    {
        var accessToken = await GetOrRefreshOwnerAccessTokenAsync(owner, ct);
        return string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new GraphAccessTokenSession(accessToken, refreshCt => ForceRefreshAsync(owner, refreshCt));
    }

    private async Task<GraphAccessTokenSession?> CreateInstanceTokenSessionAsync(
        CalendarSourceInstanceContext instance,
        CancellationToken ct)
    {
        var secretData = ParseSecretData(instance.SecretDataJson);
        if (secretData is null || string.IsNullOrWhiteSpace(secretData.ProtectedAccessToken))
            return null;

        if (!TryUnprotectInstanceAccessToken(instance, secretData, out var accessToken))
            return null;

        var sessionState = new GraphInstanceSessionState(secretData);
        accessToken = await EnsureFreshInstanceAccessTokenAsync(instance, sessionState, accessToken!, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        return new GraphAccessTokenSession(accessToken, refreshCt => ForceRefreshAsync(instance, sessionState, refreshCt));
    }

    private bool TryUnprotectInstanceAccessToken(
        CalendarSourceInstanceContext instance,
        GraphSourceSecretData secretData,
        out string? accessToken)
    {
        try
        {
            accessToken = _tokenProtector.Unprotect(secretData.ProtectedAccessToken!);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Unable to read Graph access token for calendar source instance {CalendarSourceInstanceId}; returning no events.",
                instance.Id);
            accessToken = null;
            return false;
        }
    }

    private async Task<string> EnsureFreshInstanceAccessTokenAsync(
        CalendarSourceInstanceContext instance,
        GraphInstanceSessionState sessionState,
        string accessToken,
        CancellationToken ct)
    {
        var expiresAt = sessionState.SecretData.TokenExpiresAtUtc;
        if (expiresAt is null || expiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(1))
            return accessToken;

        return await ForceRefreshAsync(instance, sessionState, ct);
    }

    private async Task<string> RefreshIfExpiringAsync(CalendarOwner owner, string accessToken, CancellationToken ct)
    {
        var expiresAt = owner.GraphTokenExpiresAtUtc;
        if (expiresAt is null || expiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(1))
            return accessToken;

        return await ForceRefreshAsync(owner, ct);
    }

    private async Task<string> ForceRefreshAsync(CalendarOwner owner, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(owner.GraphRefreshTokenProtected))
        {
            _logger.LogWarning(
                "Graph access token refresh skipped for calendar owner {CalendarOwnerId}: no refresh token available.",
                owner.Id);
            return string.Empty;
        }

        string refreshToken;
        try
        {
            refreshToken = _tokenProtector.Unprotect(owner.GraphRefreshTokenProtected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Graph access token refresh failed for calendar owner {CalendarOwnerId}: refresh token could not be read.",
                owner.Id);
            return string.Empty;
        }

        try
        {
            var refreshed = await _tokenClient.RefreshAccessTokenAsync(refreshToken, owner.GraphGrantedScopes, ct);
            owner.GraphAccessTokenProtected = _tokenProtector.Protect(refreshed.AccessToken);
            if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                owner.GraphRefreshTokenProtected = _tokenProtector.Protect(refreshed.RefreshToken);

            if (!string.IsNullOrWhiteSpace(refreshed.Scope))
                owner.GraphGrantedScopes = refreshed.Scope;

            owner.GraphTokenExpiresAtUtc = refreshed.ExpiresAtUtc;
            owner.GraphTokenLastRefreshedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            return refreshed.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Graph access token refresh failed for calendar owner {CalendarOwnerId}; returning no events.",
                owner.Id);
            return string.Empty;
        }
    }

    private async Task<string> ForceRefreshAsync(
        CalendarSourceInstanceContext instance,
        GraphInstanceSessionState sessionState,
        CancellationToken ct)
    {
        var refreshResult = await ForceRefreshAsync(instance, sessionState.SecretData, ct);
        sessionState.SecretData = refreshResult.SecretData;
        return refreshResult.AccessToken;
    }

    private async Task<GraphTokenRefreshResult> ForceRefreshAsync(
        CalendarSourceInstanceContext instance,
        GraphSourceSecretData secretData,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secretData.ProtectedRefreshToken))
        {
            _logger.LogWarning(
                "Graph access token refresh skipped for calendar source instance {CalendarSourceInstanceId}: no refresh token available.",
                instance.Id);
            return GraphTokenRefreshResult.Empty(secretData);
        }

        string refreshToken;
        try
        {
            refreshToken = _tokenProtector.Unprotect(secretData.ProtectedRefreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Graph access token refresh failed for calendar source instance {CalendarSourceInstanceId}: refresh token could not be read.",
                instance.Id);
            return GraphTokenRefreshResult.Empty(secretData);
        }

        try
        {
            var refreshed = await _tokenClient.RefreshAccessTokenAsync(refreshToken, secretData.GrantedScopes, ct);
            var updatedSecretData = secretData with
            {
                ProtectedAccessToken = _tokenProtector.Protect(refreshed.AccessToken),
                ProtectedRefreshToken = string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                    ? secretData.ProtectedRefreshToken
                    : _tokenProtector.Protect(refreshed.RefreshToken),
                GrantedScopes = string.IsNullOrWhiteSpace(refreshed.Scope) ? secretData.GrantedScopes : refreshed.Scope,
                TokenExpiresAtUtc = refreshed.ExpiresAtUtc,
                TokenLastRefreshedAtUtc = DateTimeOffset.UtcNow
            };

            await _calendarSourceInstanceStore.UpdateSecretDataAsync(
                instance.CalendarOwnerId,
                instance.Id,
                JsonSerializer.Serialize(updatedSecretData),
                ct);

            return new GraphTokenRefreshResult(refreshed.AccessToken, updatedSecretData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Graph access token refresh failed for calendar source instance {CalendarSourceInstanceId}; returning no events.",
                instance.Id);
            return GraphTokenRefreshResult.Empty(secretData);
        }
    }

    private string BuildCalendarViewRequestUri(
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var expand = $"singleValueExtendedProperties($filter=id eq '{ManagedPropertyId}')";
        return
            $"{GraphCalendarViewPath}?startDateTime={Uri.EscapeDataString(from.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}&endDateTime={Uri.EscapeDataString(to.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}&$expand={Uri.EscapeDataString(expand)}&$top={GraphCalendarViewPageSize}";
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string requestUri,
        string accessToken,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");

        return await _httpClient.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetWithRetryAsync(
        string requestUri,
        GraphAccessTokenSession tokenSession,
        CancellationToken ct)
    {
        var response = await SendAuthorizedGetAsync(requestUri, tokenSession.AccessToken, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();

        if (!await tokenSession.TryRefreshAsync(ct))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        return await SendAuthorizedGetAsync(requestUri, tokenSession.AccessToken, ct);
    }

    /// <summary>Follows @odata.nextLink pages until exhausted and returns all events.</summary>
    private async Task<List<GraphEvent>> CollectAllPagesAsync(
        GraphCalendarViewResponse? firstPage,
        GraphAccessTokenSession tokenSession,
        CancellationToken ct)
    {
        var allEvents = new List<GraphEvent>();
        var page = firstPage;
        var seenNextLinks = new HashSet<string>(StringComparer.Ordinal);

        while (page is not null)
        {
            allEvents.AddRange(page.Value ?? []);

            if (string.IsNullOrWhiteSpace(page.NextLink))
                break;

            if (!seenNextLinks.Add(page.NextLink))
            {
                _logger.LogWarning(
                    "Graph calendarView pagination returned a repeated nextLink; stopping early to avoid an infinite loop.");
                break;
            }

            page = await FetchNextPageAsync(tokenSession, page.NextLink, ct);
        }

        return allEvents;
    }

    private async Task<GraphCalendarViewResponse?> FetchNextPageAsync(
        GraphAccessTokenSession tokenSession,
        string nextLink,
        CancellationToken ct)
    {
        using var response = await SendAuthorizedGetWithRetryAsync(nextLink, tokenSession, ct);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<GraphCalendarViewResponse>(cancellationToken: ct);
        _logger.LogWarning(
            "Graph calendarView next-page fetch failed with HTTP {StatusCode}; pagination stopped early.",
            (int)response.StatusCode);
        return null;

    }

    private async Task<HttpResponseMessage> GetCalendarViewWithRetryAsync(
        GraphAccessTokenSession tokenSession,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
        => await SendAuthorizedGetWithRetryAsync(BuildCalendarViewRequestUri(from, to), tokenSession, ct);

    private static GraphSourceSecretData? ParseSecretData(string? secretDataJson)
    {
        if (string.IsNullOrWhiteSpace(secretDataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GraphSourceSecretData>(secretDataJson);
        }
        catch
        {
            return null;
        }
    }

    private CalendarEvent? MapEvent(GraphEvent source)
    {
        if (!TryParseGraphDateTime(source.Start, out var start)
            || !TryParseGraphDateTime(source.End, out var end)
            || end <= start)
        {
            return null;
        }

        var attendees = source.Attendees?
            .Select(attendee => attendee.EmailAddress?.Address)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Cast<string>()
            .ToArray() ?? [];

        var id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id;
        var title = string.IsNullOrWhiteSpace(source.Subject) ? "Busy" : source.Subject;

        return new CalendarEvent(
            id,
            title,
            source.BodyPreview,
            start,
            end,
            attendees,
            source.Location?.DisplayName);
    }

    private static bool IsManagedEvent(GraphEvent source)
        => source.ExtendedProperties?.Any(property =>
               string.Equals(property.Id, ManagedPropertyId, StringComparison.Ordinal)
               && string.Equals(property.Value, "1", StringComparison.Ordinal))
           == true;

    private bool TryParseGraphDateTime(GraphDateTimeTimeZone? source, out DateTimeOffset value)
    {
        value = default;
        if (source is null || string.IsNullOrWhiteSpace(source.DateTime))
            return false;

        if (DateTimeOffset.TryParse(source.DateTime, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedOffset))
        {
            value = parsedOffset;
            return true;
        }

        if (!DateTime.TryParse(source.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsedDateTime))
            return false;

        var timeZoneId = source.TimeZone;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                value = TimeZoneInfo.ConvertTimeToUtc(parsedDateTime, timeZone);
                return true;
            }
            catch (TimeZoneNotFoundException ex)
            {
                _logger.LogDebug(ex,
                    "Graph event timezone '{TimeZoneId}' was not found on this host. Falling back to UTC parsing.",
                    timeZoneId);
            }
            catch (InvalidTimeZoneException ex)
            {
                _logger.LogDebug(ex,
                    "Graph event timezone '{TimeZoneId}' is invalid on this host. Falling back to UTC parsing.",
                    timeZoneId);
            }
        }

        value = new DateTimeOffset(DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc));
        return true;
    }

    private static bool AllowsWriteBack(string? grantedScopes)
        => string.IsNullOrWhiteSpace(grantedScopes)
           || grantedScopes.Contains("Calendars.ReadWrite", StringComparison.OrdinalIgnoreCase);
}
