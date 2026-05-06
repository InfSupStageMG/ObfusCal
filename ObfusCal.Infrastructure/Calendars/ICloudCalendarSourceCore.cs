using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Calendars;

public sealed class ICloudCalendarSourceCore(
    HttpClient httpClient,
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<ICloudCalendarOptions> options,
    ILogger<ICloudCalendarSourceCore> logger)
{
    private readonly IDataProtector _credentialProtector = dataProtectionProvider
        .CreateProtector("ObfusCal.ICloudCalendar.Credentials.v1");

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

        var owner = await dbContext.CalendarOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId.Value, ct);

        if (owner is null)
            return [];

        var configuration = TryBuildConfiguration(owner);
        if (configuration is null)
            return [];

        var result = await QueryCalendarAsync(owner.Id, configuration, from, to, includeEvents: true, ct);
        return result.Events;
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

        var configuration = TryBuildConfiguration(instance);
        if (configuration is null)
            return [];

        var result =
            await QueryCalendarAsync(instance.CalendarOwnerId, configuration, from, to, includeEvents: true, ct);
        return result.Events;
    }

    public async Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var owner = await dbContext.CalendarOwners
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == calendarOwnerId, ct);

        if (owner is null)
            return CalendarSourceReadiness.NotReady("Calendar owner not found.");

        if (string.IsNullOrWhiteSpace(owner.ICloudCalendarUrl)
            || string.IsNullOrWhiteSpace(owner.ICloudAppleIdProtected)
            || string.IsNullOrWhiteSpace(owner.ICloudAppSpecificPasswordProtected))
        {
            return CalendarSourceReadiness.NotReady(
                "iCloud Calendar configuration required.",
                "Configure the iCloud calendar URL, Apple ID, and app-specific password before requesting busy slots.");
        }

        var configuration = TryBuildConfiguration(owner);
        if (configuration is null)
        {
            return CalendarSourceReadiness.NotReady(
                "iCloud Calendar credentials could not be read.",
                "Re-save the iCloud calendar configuration for this calendar owner.");
        }

        var probeFrom = DateTimeOffset.UtcNow;
        var lookAheadDays = Math.Max(1, options.Value.ReadinessProbeLookAheadDays);
        var probeTo = probeFrom.AddDays(lookAheadDays);
        var result = await QueryCalendarAsync(owner.Id, configuration, probeFrom, probeTo, includeEvents: false, ct);

        return result.Status switch
        {
            ICloudCalendarQueryStatus.Success => CalendarSourceReadiness.Ready("iCloud Calendar is configured."),
            ICloudCalendarQueryStatus.AuthenticationFailed => CalendarSourceReadiness.NotReady(
                "iCloud authentication failed.",
                "Verify the Apple ID and app-specific password for this calendar owner."),
            ICloudCalendarQueryStatus.Unreachable => CalendarSourceReadiness.NotReady(
                "iCloud CalDAV endpoint is unreachable.",
                "Check the configured iCloud calendar URL and network connectivity."),
            ICloudCalendarQueryStatus.NotConfigured => CalendarSourceReadiness.NotReady(
                "iCloud Calendar configuration required.",
                "Configure the iCloud calendar URL, Apple ID, and app-specific password before requesting busy slots."),
            _ => CalendarSourceReadiness.NotReady(
                "iCloud Calendar request failed.",
                "The configured iCloud calendar could not be queried successfully.")
        };
    }

    public async Task<CalendarSourceReadiness> GetReadinessAsync(CalendarSourceInstanceContext instance,
        CancellationToken ct = default)
    {
        var configuration = TryBuildConfiguration(instance);
        if (configuration is null)
        {
            return CalendarSourceReadiness.NotReady(
                "iCloud Calendar configuration required.",
                "Configure the iCloud calendar URL, Apple ID, and app-specific password for this source instance.");
        }

        var probeFrom = DateTimeOffset.UtcNow;
        var lookAheadDays = Math.Max(1, options.Value.ReadinessProbeLookAheadDays);
        var probeTo = probeFrom.AddDays(lookAheadDays);
        var result = await QueryCalendarAsync(instance.CalendarOwnerId, configuration, probeFrom, probeTo,
            includeEvents: false, ct);

        return result.Status switch
        {
            ICloudCalendarQueryStatus.Success => CalendarSourceReadiness.Ready("iCloud Calendar is configured."),
            ICloudCalendarQueryStatus.AuthenticationFailed => CalendarSourceReadiness.NotReady(
                "iCloud authentication failed.",
                "Verify the Apple ID and app-specific password for this source instance."),
            ICloudCalendarQueryStatus.Unreachable => CalendarSourceReadiness.NotReady(
                "iCloud CalDAV endpoint is unreachable.",
                "Check the configured iCloud calendar URL and network connectivity."),
            _ => CalendarSourceReadiness.NotReady(
                "iCloud Calendar request failed.",
                "The configured iCloud calendar could not be queried successfully.")
        };
    }

    private ICloudCalendarOwnerConfiguration? TryBuildConfiguration(CalendarOwner owner)
    {
        if (string.IsNullOrWhiteSpace(owner.ICloudCalendarUrl)
            || string.IsNullOrWhiteSpace(owner.ICloudAppleIdProtected)
            || string.IsNullOrWhiteSpace(owner.ICloudAppSpecificPasswordProtected)
            || !Uri.TryCreate(owner.ICloudCalendarUrl, UriKind.Absolute, out var calendarUri))
        {
            return null;
        }

        try
        {
            return new ICloudCalendarOwnerConfiguration(
                calendarUri,
                _credentialProtector.Unprotect(owner.ICloudAppleIdProtected),
                _credentialProtector.Unprotect(owner.ICloudAppSpecificPasswordProtected));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Unable to read protected iCloud credentials for calendar owner {CalendarOwnerId}.",
                owner.Id);
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private ICloudCalendarOwnerConfiguration? TryBuildConfiguration(CalendarSourceInstanceContext instance)
    {
        var configuration = ParseConfiguration(instance.ConfigurationJson);
        var secrets = ParseSecretData(instance.SecretDataJson);

        if (configuration is null
            || secrets is null
            || string.IsNullOrWhiteSpace(configuration.CalendarUrl)
            || string.IsNullOrWhiteSpace(secrets.AppleId)
            || string.IsNullOrWhiteSpace(secrets.AppSpecificPassword)
            || !Uri.TryCreate(configuration.CalendarUrl, UriKind.Absolute, out var calendarUri))
        {
            return null;
        }

        return new ICloudCalendarOwnerConfiguration(calendarUri, secrets.AppleId, secrets.AppSpecificPassword);
    }

    private async Task<ICloudCalendarQueryResult> QueryCalendarAsync(
        Guid calendarOwnerId,
        ICloudCalendarOwnerConfiguration configuration,
        DateTimeOffset from,
        DateTimeOffset to,
        bool includeEvents,
        CancellationToken ct)
    {
        try
        {
            using var request = CreateCalendarQueryRequest(configuration, from, to);
            using var response = await httpClient.SendAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogWarning(
                    "iCloud CalDAV authentication failed for calendar owner {CalendarOwnerId}.",
                    calendarOwnerId);
                return new ICloudCalendarQueryResult(ICloudCalendarQueryStatus.AuthenticationFailed, []);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "iCloud CalDAV query failed for calendar owner {CalendarOwnerId} with HTTP {StatusCode}.",
                    calendarOwnerId,
                    (int)response.StatusCode);
                return new ICloudCalendarQueryResult(ICloudCalendarQueryStatus.Failed, []);
            }

            if (!includeEvents)
                return new ICloudCalendarQueryResult(ICloudCalendarQueryStatus.Success, []);

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var events = ParseCalDavEvents(responseBody)
                .Where(e => e.Start < to && e.End > from)
                .OrderBy(e => e.Start)
                .ToList();

            return new ICloudCalendarQueryResult(ICloudCalendarQueryStatus.Success, events);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "iCloud CalDAV endpoint could not be reached for calendar owner {CalendarOwnerId}.",
                calendarOwnerId);
            return new ICloudCalendarQueryResult(ICloudCalendarQueryStatus.Unreachable, []);
        }
    }

    private static HttpRequestMessage CreateCalendarQueryRequest(
        ICloudCalendarOwnerConfiguration configuration,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var request = new HttpRequestMessage(new HttpMethod("REPORT"), configuration.CalendarUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{configuration.AppleId}:{configuration.AppSpecificPassword}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.TryAddWithoutValidation("Depth", "1");

        var startStamp = from.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var endStamp = to.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        var body = $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <c:calendar-query xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
                      <d:prop>
                        <d:getetag />
                        <c:calendar-data>
                          <c:expand start="{startStamp}" end="{endStamp}" />
                        </c:calendar-data>
                      </d:prop>
                      <c:filter>
                        <c:comp-filter name="VCALENDAR">
                          <c:comp-filter name="VEVENT">
                            <c:time-range start="{startStamp}" end="{endStamp}" />
                          </c:comp-filter>
                        </c:comp-filter>
                      </c:filter>
                    </c:calendar-query>
                    """;


        request.Content = new StringContent(body, Encoding.UTF8, "application/xml");
        return request;
    }

    private static IReadOnlyList<CalendarEvent> ParseCalDavEvents(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return [];

        var document = XDocument.Parse(responseBody);
        var events = new List<CalendarEvent>();

        foreach (var calendarData in document
                     .Descendants()
                     .Where(element => string.Equals(element.Name.LocalName, "calendar-data",
                         StringComparison.OrdinalIgnoreCase))
                     .Select(element => element.Value)
                     .Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            events.AddRange(IcsCalendarEventParser.ParseEvents(calendarData));
        }

        return events;
    }

    private static ICloudCalendarInstanceConfiguration? ParseConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ICloudCalendarInstanceConfiguration>(configurationJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static ICloudCalendarInstanceSecretData? ParseSecretData(string? secretDataJson)
    {
        if (string.IsNullOrWhiteSpace(secretDataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ICloudCalendarInstanceSecretData>(secretDataJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private sealed record ICloudCalendarOwnerConfiguration(
        Uri CalendarUri,
        string AppleId,
        string AppSpecificPassword);

    internal sealed record ICloudCalendarInstanceConfiguration(
        [property: JsonPropertyName("calendarUrl")] string CalendarUrl);

    internal sealed record ICloudCalendarInstanceSecretData(
        [property: JsonPropertyName("appleId")] string AppleId,
        [property: JsonPropertyName("appSpecificPassword")] string AppSpecificPassword);

    private sealed record ICloudCalendarQueryResult(
        ICloudCalendarQueryStatus Status,
        IReadOnlyList<CalendarEvent> Events);

    private enum ICloudCalendarQueryStatus
    {
        Success,
        NotConfigured,
        AuthenticationFailed,
        Unreachable,
        Failed
    }
}
