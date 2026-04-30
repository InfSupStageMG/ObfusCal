using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Calendars;

[CalendarSourcePlugin("graph", "Microsoft Graph")]
public sealed class GraphCalendarSource(
    HttpClient httpClient,
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    IGraphOAuthTokenClient tokenClient,
    MockCalendarSource fallbackSource,
    IHostEnvironment hostEnvironment,
    ILogger<GraphCalendarSource> logger) : ICalendarSource, ICalendarSourceReadinessEvaluator
{
    private const string GraphCalendarViewPath = "v1.0/me/calendarView";

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
            return await ResolveFallbackAsync(from, to, null, ct);

        var owner = await dbContext.CalendarOwners
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId.Value, ct);

        if (owner is null)
            return [];

        if (string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected))
            return await ResolveFallbackAsync(from, to, calendarOwnerId, ct);

        string accessToken;
        try
        {
            accessToken = _tokenProtector.Unprotect(owner.GraphAccessTokenProtected);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Unable to read Graph access token for calendar owner {CalendarOwnerId}; returning no events.",
                calendarOwnerId.Value);
            return [];
        }

        accessToken = await RefreshIfExpiringAsync(owner, accessToken, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
            return [];

        using var response = await GetCalendarViewWithRetryAsync(owner, accessToken, from, to, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Graph calendar fetch failed for calendar owner {CalendarOwnerId} with HTTP {StatusCode}.",
                calendarOwnerId.Value,
                (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<GraphCalendarViewResponse>(cancellationToken: ct);
        var events = payload?.Value ?? [];

        return events
            .Select(MapEvent)
            .Where(mapped => mapped is not null)
            .Select(mapped => mapped!)
            .Where(e => e.Start < to && e.End > from)
            .OrderBy(e => e.Start)
            .ToList();
    }

    public async Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var owner = await dbContext.CalendarOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId, ct);

        if (owner is null)
            return CalendarSourceReadiness.NotReady("Calendar owner not found.");

        var hasConsent = !string.IsNullOrWhiteSpace(owner.GraphAccessTokenProtected)
            || !string.IsNullOrWhiteSpace(owner.GraphRefreshTokenProtected);

        return hasConsent
            ? CalendarSourceReadiness.Ready("Microsoft Graph calendar is configured.")
            : CalendarSourceReadiness.NotReady(
                "Microsoft Graph consent required.",
                "This calendar owner has not granted Microsoft Graph calendar consent yet. Complete consent before requesting busy slots.");
    }

    private async Task<IReadOnlyList<CalendarEvent>> ResolveFallbackAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? calendarOwnerId,
        CancellationToken ct)
    {
        if (!hostEnvironment.IsDevelopment())
            return [];

        return await fallbackSource.GetEventsAsync(from, to, calendarOwnerId, ct);
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
            logger.LogWarning(
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
            logger.LogWarning(ex,
                "Graph access token refresh failed for calendar owner {CalendarOwnerId}: refresh token could not be read.",
                owner.Id);
            return string.Empty;
        }

        try
        {
            var refreshed = await tokenClient.RefreshAccessTokenAsync(refreshToken, ct);
            owner.GraphAccessTokenProtected = _tokenProtector.Protect(refreshed.AccessToken);
            if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                owner.GraphRefreshTokenProtected = _tokenProtector.Protect(refreshed.RefreshToken);

            owner.GraphTokenExpiresAtUtc = refreshed.ExpiresAtUtc;
            owner.GraphTokenLastRefreshedAtUtc = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(ct);
            return refreshed.AccessToken;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Graph access token refresh failed for calendar owner {CalendarOwnerId}; returning no events.",
                owner.Id);
            return string.Empty;
        }
    }

    private async Task<HttpResponseMessage> GetCalendarViewAsync(
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var requestUri =
            $"{GraphCalendarViewPath}?startDateTime={Uri.EscapeDataString(from.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}&endDateTime={Uri.EscapeDataString(to.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");

        return await httpClient.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> GetCalendarViewWithRetryAsync(
        CalendarOwner owner,
        string accessToken,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var response = await GetCalendarViewAsync(accessToken, from, to, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();

        var refreshedAccessToken = await ForceRefreshAsync(owner, ct);
        if (string.IsNullOrWhiteSpace(refreshedAccessToken))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        return await GetCalendarViewAsync(refreshedAccessToken, from, to, ct);
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
                logger.LogDebug(ex,
                    "Graph event timezone '{TimeZoneId}' was not found on this host. Falling back to UTC parsing.",
                    timeZoneId);
            }
            catch (InvalidTimeZoneException ex)
            {
                logger.LogDebug(ex,
                    "Graph event timezone '{TimeZoneId}' is invalid on this host. Falling back to UTC parsing.",
                    timeZoneId);
            }
        }

        value = new DateTimeOffset(DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc));
        return true;
    }

    private sealed record GraphCalendarViewResponse([property: JsonPropertyName("value")] List<GraphEvent>? Value);

    private sealed record GraphEvent(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("subject")]
        string? Subject,
        [property: JsonPropertyName("bodyPreview")]
        string? BodyPreview,
        [property: JsonPropertyName("start")] GraphDateTimeTimeZone? Start,
        [property: JsonPropertyName("end")] GraphDateTimeTimeZone? End,
        [property: JsonPropertyName("attendees")]
        List<GraphAttendee>? Attendees,
        [property: JsonPropertyName("location")]
        GraphLocation? Location);

    private sealed record GraphDateTimeTimeZone(
        [property: JsonPropertyName("dateTime")]
        string? DateTime,
        [property: JsonPropertyName("timeZone")]
        string? TimeZone);

    private sealed record GraphAttendee(
        [property: JsonPropertyName("emailAddress")]
        GraphEmailAddress? EmailAddress);

    private sealed record GraphEmailAddress(
        [property: JsonPropertyName("address")]
        string? Address);

    private sealed record GraphLocation(
        [property: JsonPropertyName("displayName")]
        string? DisplayName);
}


