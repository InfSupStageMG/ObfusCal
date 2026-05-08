using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
    ICalendarSourceSecretProtector secretProtector,
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

        var configuration = await TryBuildConfigurationAsync(owner, ct);
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

        var configuration = await TryBuildConfigurationAsync(instance, ct);
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

        var configuration = await TryBuildConfigurationAsync(owner, ct);
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
        var configuration = await TryBuildConfigurationAsync(instance, ct);
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

    private async Task<ICloudCalendarOwnerConfiguration?> TryBuildConfigurationAsync(CalendarOwner owner,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(owner.ICloudCalendarUrl)
            || string.IsNullOrWhiteSpace(owner.ICloudAppleIdProtected)
            || string.IsNullOrWhiteSpace(owner.ICloudAppSpecificPasswordProtected)
            || !Uri.TryCreate(owner.ICloudCalendarUrl, UriKind.Absolute, out var calendarUri))
        {
            return null;
        }

        var appleId = TryUnprotectWithLegacyPlaintextFallback(
            owner.ICloudAppleIdProtected,
            _credentialProtector.Unprotect,
            secretProtector.Unprotect,
            "Apple ID",
            "calendar owner",
            owner.Id);
        var appSpecificPassword = TryUnprotectWithLegacyPlaintextFallback(
            owner.ICloudAppSpecificPasswordProtected,
            _credentialProtector.Unprotect,
            secretProtector.Unprotect,
            "app-specific password",
            "calendar owner",
            owner.Id);

        if (string.IsNullOrWhiteSpace(appleId.Value) || string.IsNullOrWhiteSpace(appSpecificPassword.Value))
            return null;

        if (appleId.NeedsReprotect || appSpecificPassword.NeedsReprotect)
            await TryMigrateOwnerCredentialsAsync(owner.Id, appleId.Value, appSpecificPassword.Value, ct);

        return new ICloudCalendarOwnerConfiguration(calendarUri, appleId.Value, appSpecificPassword.Value);
    }

    private async Task<ICloudCalendarOwnerConfiguration?> TryBuildConfigurationAsync(
        CalendarSourceInstanceContext instance,
        CancellationToken ct)
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

        // Instance contexts are typically already decrypted by CalendarSourceInstanceService.
        // Only migrate when the value was protected with a non-primary protector or when
        // the persisted SecretDataJson blob is still in a legacy/raw JSON format at rest.
        var appleId = TryReadInstanceCredential(
            secrets.AppleId,
            "Apple ID",
            instance.Id);
        var appSpecificPassword = TryReadInstanceCredential(
            secrets.AppSpecificPassword,
            "app-specific password",
            instance.Id);

        if (string.IsNullOrWhiteSpace(appleId.Value) || string.IsNullOrWhiteSpace(appSpecificPassword.Value))
            return null;

        var needsBlobMigration = await ShouldMigrateInstanceSecretStorageAsync(instance.Id, ct);
        if (appleId.NeedsReprotect || appSpecificPassword.NeedsReprotect || needsBlobMigration)
            await TryMigrateInstanceCredentialsAsync(instance.Id, configuration, appleId.Value, appSpecificPassword.Value, ct);

        return new ICloudCalendarOwnerConfiguration(calendarUri, appleId.Value, appSpecificPassword.Value);
    }

    private CredentialReadResult TryReadInstanceCredential(
        string protectedOrPlaintext,
        string credentialKind,
        Guid instanceId)
    {
        if (TryUnprotect(secretProtector.Unprotect, protectedOrPlaintext, out var value, out var expectedFailure, out var unexpectedFailure))
            return new CredentialReadResult(value, false);

        if (TryUnprotect(_credentialProtector.Unprotect, protectedOrPlaintext, out value, out _, out _))
            return new CredentialReadResult(value, true);

        if (unexpectedFailure is not null)
        {
            logger.LogWarning(unexpectedFailure,
                "Unable to read iCloud {CredentialKind} for calendar source instance {CalendarSourceInstanceId}.",
                credentialKind,
                instanceId);
            return new CredentialReadResult(null, false);
        }

        // Expected unprotect failures are normal for already-decrypted instance context values.
        // Treat as plaintext-in-memory and let at-rest blob inspection decide if migration is needed.
        if (expectedFailure)
            return new CredentialReadResult(protectedOrPlaintext, false);

        return new CredentialReadResult(null, false);
    }

    private async Task<bool> ShouldMigrateInstanceSecretStorageAsync(Guid instanceId, CancellationToken ct)
    {
        var storedSecretData = await dbContext.CalendarSourceInstances
            .AsNoTracking()
            .Where(x => x.Id == instanceId)
            .Select(x => x.SecretDataJson)
            .SingleOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(storedSecretData))
            return false;

        try
        {
            secretProtector.Unprotect(storedSecretData);
            return false;
        }
        catch (Exception ex) when (IsUnprotectFailure(ex))
        {
            // Legacy/raw JSON at rest should be migrated to protected storage.
            return storedSecretData.TrimStart().StartsWith('{');
        }
        catch
        {
            // Unknown failure (e.g., key ring mismatch): avoid destructive rewrites.
            return false;
        }
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
            var rawVeventCount = CountOccurrences(responseBody, "BEGIN:VEVENT");
            var parsedEvents = ParseCalDavEvents(responseBody);

            if (rawVeventCount > 0 && parsedEvents.Count == 0)
            {
                logger.LogWarning(
                    "iCloud CalDAV returned {RawVeventCount} VEVENT block(s), but none could be parsed for calendar owner {CalendarOwnerId}.",
                    rawVeventCount,
                    calendarOwnerId);
            }

            var events = parsedEvents
                .Where(e => e.Start < to && e.End > from)
                .OrderBy(e => e.Start)
                .ToList();

            if (events.Count == 0 && parsedEvents.Count > 0)
            {
                // CalDAV request already applies server-side time-range filtering. If local overlap math
                // drops everything (for example due to floating-time interpretation), prefer parsed events.
                logger.LogWarning(
                    "iCloud local overlap filter removed all {ParsedEventCount} parsed event(s) for calendar owner {CalendarOwnerId}. Falling back to server-filtered parsed events.",
                    parsedEvents.Count,
                    calendarOwnerId);
                events = parsedEvents.OrderBy(e => e.Start).ToList();
            }

            if (events.Count == 0)
            {
                logger.LogInformation(
                    "iCloud CalDAV returned 0 overlapping event(s) for calendar owner {CalendarOwnerId} in range [{FromUtc}, {ToUtc}). Raw VEVENTs: {RawVeventCount}, parsed events: {ParsedEventCount}.",
                    calendarOwnerId,
                    from,
                    to,
                    rawVeventCount,
                    parsedEvents.Count);
            }

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
        [property: JsonPropertyName("calendarUrl")]
        string CalendarUrl);

    internal sealed record ICloudCalendarInstanceSecretData(
        [property: JsonPropertyName("appleId")]
        string AppleId,
        [property: JsonPropertyName("appSpecificPassword")]
        string AppSpecificPassword);

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

    private CredentialReadResult TryUnprotectWithLegacyPlaintextFallback(
        string protectedOrPlaintext,
        Func<string, string> primaryUnprotect,
        Func<string, string>? secondaryUnprotect,
        string credentialKind,
        string subjectKind,
        Guid subjectId)
    {
        var hadExpectedUnprotectFailure = false;
        Exception? unexpectedFailure = null;

        if (TryUnprotect(primaryUnprotect, protectedOrPlaintext, out var value, out var expectedFailure, out var unexpected))
            return new CredentialReadResult(value, false);

        hadExpectedUnprotectFailure |= expectedFailure;
        unexpectedFailure ??= unexpected;

        if (secondaryUnprotect is not null
            && TryUnprotect(secondaryUnprotect, protectedOrPlaintext, out value, out expectedFailure, out unexpected))
        {
            return new CredentialReadResult(value, true);
        }

        hadExpectedUnprotectFailure |= expectedFailure;
        unexpectedFailure ??= unexpected;

        if (hadExpectedUnprotectFailure)
        {
            logger.LogInformation(
                "Using legacy plaintext iCloud {CredentialKind} for {SubjectKind} {SubjectId}. Re-save configuration to re-protect credentials.",
                credentialKind,
                subjectKind,
                subjectId);
            return new CredentialReadResult(protectedOrPlaintext, true);
        }

        if (unexpectedFailure is not null)
        {
            logger.LogWarning(unexpectedFailure,
                "Unable to read iCloud {CredentialKind} for {SubjectKind} {SubjectId}.",
                credentialKind,
                subjectKind,
                subjectId);
        }

        return new CredentialReadResult(null, false);
    }

    private static bool TryUnprotect(
        Func<string, string> unprotect,
        string input,
        out string value,
        out bool expectedFailure,
        out Exception? unexpectedFailure)
    {
        try
        {
            value = unprotect(input);
            expectedFailure = false;
            unexpectedFailure = null;
            return true;
        }
        catch (Exception ex) when (IsUnprotectFailure(ex))
        {
            value = string.Empty;
            expectedFailure = true;
            unexpectedFailure = null;
            return false;
        }
        catch (Exception ex)
        {
            value = string.Empty;
            expectedFailure = false;
            unexpectedFailure = ex;
            return false;
        }
    }

    private async Task TryMigrateOwnerCredentialsAsync(
        Guid ownerId,
        string appleId,
        string appSpecificPassword,
        CancellationToken ct)
    {
        try
        {
            var owner = await dbContext.CalendarOwners.SingleOrDefaultAsync(x => x.Id == ownerId, ct);
            if (owner is null)
                return;

            owner.ICloudAppleIdProtected = _credentialProtector.Protect(appleId);
            owner.ICloudAppSpecificPasswordProtected = _credentialProtector.Protect(appSpecificPassword);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to auto-migrate legacy iCloud credentials for calendar owner {CalendarOwnerId}.",
                ownerId);
        }
    }

    private async Task TryMigrateInstanceCredentialsAsync(
        Guid instanceId,
        ICloudCalendarInstanceConfiguration configuration,
        string appleId,
        string appSpecificPassword,
        CancellationToken ct)
    {
        try
        {
            var instance = await dbContext.CalendarSourceInstances.SingleOrDefaultAsync(x => x.Id == instanceId, ct);
            if (instance is null)
                return;

            var secretJson = JsonSerializer.Serialize(
                new ICloudCalendarInstanceSecretData(appleId, appSpecificPassword),
                JsonOptions);
            instance.SecretDataJson = secretProtector.Protect(secretJson);

            if (string.IsNullOrWhiteSpace(instance.ConfigurationJson))
            {
                instance.ConfigurationJson = JsonSerializer.Serialize(
                    new ICloudCalendarInstanceConfiguration(configuration.CalendarUrl),
                    JsonOptions);
            }

            instance.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Auto-migrated iCloud credentials for calendar source instance {CalendarSourceInstanceId} to protected storage.",
                instanceId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to auto-migrate legacy iCloud credentials for calendar source instance {CalendarSourceInstanceId}.",
                instanceId);
        }
    }

    private static bool IsUnprotectFailure(Exception ex)
        => ex is CryptographicException
           || ex is FormatException
           || ex.InnerException is CryptographicException
           || ex.InnerException is FormatException;

    private static int CountOccurrences(string input, string token)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(token))
            return 0;

        var count = 0;
        var start = 0;
        while (start < input.Length)
        {
            var index = input.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                break;

            count++;
            start = index + token.Length;
        }

        return count;
    }

    private readonly record struct CredentialReadResult(string? Value, bool NeedsReprotect);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}
