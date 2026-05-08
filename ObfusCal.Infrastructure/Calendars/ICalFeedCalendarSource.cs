using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Infrastructure.Calendars;

/// <summary>
/// Calendar source that imports events from one or more owner-configured iCal feed URLs.
/// Each feed is fetched independently; failure on one feed does not prevent others from loading.
/// Falls back to <see cref="MockCalendarSource"/> when the owner has no feeds configured.
/// </summary>

[CalendarSourcePlugin("ical", "iCal feed")]
[CalendarSourcePluginUi(
    supportsMultipleInstances: true,
    configurationJsonTemplate: "{\"feedUrl\":\"https://calendar.example.com/feed.ics\"}",
    setupHint: "Set a feed URL in configuration JSON. No secret JSON is required.")]
public sealed class IcalFeedCalendarSource(
    HttpClient httpClient,
    AppDbContext dbContext,
    MockCalendarSource fallbackSource,
    IUrlSafetyValidator? urlSafetyValidator,
    ILogger<IcalFeedCalendarSource> logger)
    : ICalendarSource, ICalendarSourceReadinessEvaluator, ICalendarSourceInstanceHandler, ICalendarSourceInstanceReadinessEvaluator
{
    private readonly IUrlSafetyValidator effectiveUrlSafetyValidator = urlSafetyValidator ?? new UrlSafetyValidator();

    public IcalFeedCalendarSource(
        HttpClient httpClient,
        AppDbContext dbContext,
        MockCalendarSource fallbackSource,
        ILogger<IcalFeedCalendarSource> logger)
        : this(httpClient, dbContext, fallbackSource, null, logger)
    {
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(DateTimeOffset from,
        DateTimeOffset to,
        Guid? calendarOwnerId = null,
        CancellationToken ct = default)
    {
        if (from > to)
            throw new ArgumentException("The start of the query window must be before the end.", nameof(from));

        ct.ThrowIfCancellationRequested();

        if (calendarOwnerId is null)
            return await fallbackSource.GetEventsAsync(from, to, ct: ct);

        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(x => x.Id == calendarOwnerId.Value, ct);

        if (!ownerExists)
            return [];

        var feeds = await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .Where(f => f.CalendarOwnerId == calendarOwnerId.Value)
            .ToListAsync(ct);

        if (feeds.Count == 0)
            return await fallbackSource.GetEventsAsync(from, to, calendarOwnerId, ct);

        var allEvents = new List<CalendarEvent>();

        foreach (var feed in feeds)
        {
            var feedEvents = await FetchFeedEventsAsync(feed.FeedUrl, calendarOwnerId.Value, ct);
            allEvents.AddRange(feedEvents);
        }

        return allEvents
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

        var feedUrl = ParseFeedUrl(instance.ConfigurationJson);
        if (string.IsNullOrWhiteSpace(feedUrl))
            return [];

        var events = await FetchFeedEventsAsync(feedUrl, instance.CalendarOwnerId, ct);
        return events
            .Where(e => e.Start < to && e.End > from)
            .OrderBy(e => e.Start)
            .ToList();
    }

    public async Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(x => x.Id == calendarOwnerId, ct);

        if (!ownerExists)
            return CalendarSourceReadiness.NotReady("Calendar owner not found.");

        var feedCount = await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .CountAsync(feed => feed.CalendarOwnerId == calendarOwnerId, ct);

        return feedCount > 0
            ? CalendarSourceReadiness.Ready("At least one iCal feed is configured.")
            : CalendarSourceReadiness.NotReady(
                "iCal feed required.",
                "Add at least one iCal feed before requesting busy slots from the iCal provider.");
    }

    public async Task<CalendarSourceReadiness> GetReadinessAsync(CalendarSourceInstanceContext instance, CancellationToken ct = default)
    {
        var feedUrl = ParseFeedUrl(instance.ConfigurationJson);
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return CalendarSourceReadiness.NotReady(
                "iCal feed required.",
                "Configure a feed URL for this iCal source instance.");
        }

        var validation = await effectiveUrlSafetyValidator.ValidateAsync(feedUrl, ct);
        if (!validation.IsValid)
        {
            return CalendarSourceReadiness.NotReady(
                "iCal feed URL is invalid.",
                validation.Message);
        }

        return CalendarSourceReadiness.Ready("iCal feed is configured.");
    }

    private async Task<IReadOnlyList<CalendarEvent>> FetchFeedEventsAsync(
        string feedUrl,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        var validation = await effectiveUrlSafetyValidator.ValidateAsync(feedUrl, ct);
        if (!validation.IsValid)
        {
            logger.LogWarning(
                "Calendar owner {CalendarOwnerId} has an unsafe iCal feed URL: {FeedUrl}. Reason: {Reason}",
                calendarOwnerId,
                feedUrl,
                validation.Message);
            return [];
        }

        var feedUri = new Uri(feedUrl, UriKind.Absolute);

        try
        {
            using var response = await httpClient.GetAsync(feedUri, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "iCal feed fetch failed for calendar owner {CalendarOwnerId} at {FeedUrl} with HTTP {StatusCode}.",
                    calendarOwnerId,
                    feedUrl,
                    (int)response.StatusCode);
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            return IcsCalendarEventParser.ParseEvents(content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "iCal feed fetch or parse failed for calendar owner {CalendarOwnerId} at {FeedUrl}.",
                calendarOwnerId,
                feedUrl);
            return [];
        }
    }

    private static string? ParseFeedUrl(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<IcalFeedConfiguration>(configurationJson)?.FeedUrl;
        }
        catch
        {
            return null;
        }
    }

    private sealed record IcalFeedConfiguration(string FeedUrl);

}
