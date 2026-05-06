using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class CalendarOwnerIcalFeedService(AppDbContext dbContext) : ICalendarOwnerIcalFeedService
{
    public async Task<IReadOnlyList<CalendarOwnerIcalFeedItem>> ListFeedsAsync(
        Guid calendarOwnerId,
        CancellationToken ct = default)
    {
        var rawInstanceFeeds = await dbContext.CalendarSourceInstances
            .AsNoTracking()
            .Where(instance => instance.CalendarOwnerId == calendarOwnerId && instance.PluginId == "ical")
            .OrderBy(instance => instance.DisplayName)
            .Select(instance => new { instance.Id, instance.ConfigurationJson, instance.DisplayName })
            .ToListAsync(ct);

        var instanceFeeds = rawInstanceFeeds
            .Select(instance => new CalendarOwnerIcalFeedItem(
                instance.Id,
                ParseFeedUrl(instance.ConfigurationJson) ?? instance.DisplayName))
            .ToList();

        var legacyFeeds = await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .Where(feed => feed.CalendarOwnerId == calendarOwnerId)
            .OrderBy(feed => feed.FeedUrl)
            .Select(feed => new CalendarOwnerIcalFeedItem(feed.Id, feed.FeedUrl))
            .ToListAsync(ct);

        return instanceFeeds.Concat(legacyFeeds)
            .OrderBy(feed => feed.FeedUrl)
            .ToList();
    }

    public async Task<AddCalendarOwnerIcalFeedResult> AddFeedAsync(
        Guid calendarOwnerId,
        string feedUrl,
        CancellationToken ct = default)
    {
        var owner = await dbContext.CalendarOwners
            .SingleOrDefaultAsync(o => o.Id == calendarOwnerId, ct);

        if (owner is null)
            return new AddCalendarOwnerIcalFeedResult(AddCalendarOwnerIcalFeedOutcome.CalendarOwnerNotFound);

        var normalizedFeedUrl = feedUrl.Trim();

        var instanceConfigJsons = await dbContext.CalendarSourceInstances
            .AsNoTracking()
            .Where(instance => instance.CalendarOwnerId == calendarOwnerId && instance.PluginId == "ical")
            .Select(instance => instance.ConfigurationJson)
            .ToListAsync(ct);

        var alreadyExists = instanceConfigJsons.Any(json => ParseFeedUrl(json) == normalizedFeedUrl);

        alreadyExists = alreadyExists || await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .AnyAsync(feed => feed.CalendarOwnerId == calendarOwnerId && feed.FeedUrl == normalizedFeedUrl, ct);

        if (alreadyExists)
            return new AddCalendarOwnerIcalFeedResult(AddCalendarOwnerIcalFeedOutcome.Duplicate);

        var displayName = Uri.TryCreate(normalizedFeedUrl, UriKind.Absolute, out var feedUri)
            ? feedUri.Host
            : normalizedFeedUrl;

        var feed = new CalendarSourceInstance
        {
            Id = Guid.NewGuid(),
            CalendarOwnerId = calendarOwnerId,
            PluginId = "ical",
            DisplayName = displayName,
            IsEnabled = true,
            ConfigurationJson = JsonSerializer.Serialize(new IcalFeedConfiguration(normalizedFeedUrl)),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.CalendarSourceInstances.Add(feed);

        await dbContext.SaveChangesAsync(ct);

        return new AddCalendarOwnerIcalFeedResult(
            AddCalendarOwnerIcalFeedOutcome.Added,
            feed.Id,
            normalizedFeedUrl);
    }

    public async Task<DeleteCalendarOwnerIcalFeedResult> DeleteFeedAsync(
        Guid calendarOwnerId,
        Guid feedId,
        CancellationToken ct = default)
    {
        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(owner => owner.Id == calendarOwnerId, ct);

        if (!ownerExists)
            return new DeleteCalendarOwnerIcalFeedResult(DeleteCalendarOwnerIcalFeedOutcome.CalendarOwnerNotFound);

        var instance = await dbContext.CalendarSourceInstances
            .SingleOrDefaultAsync(x => x.Id == feedId && x.CalendarOwnerId == calendarOwnerId && x.PluginId == "ical", ct);

        if (instance is not null)
        {
            dbContext.CalendarSourceInstances.Remove(instance);
            await dbContext.SaveChangesAsync(ct);
            return new DeleteCalendarOwnerIcalFeedResult(DeleteCalendarOwnerIcalFeedOutcome.Deleted);
        }

        var feed = await dbContext.CalendarOwnerICalFeeds
            .SingleOrDefaultAsync(x => x.Id == feedId && x.CalendarOwnerId == calendarOwnerId, ct);

        if (feed is null)
            return new DeleteCalendarOwnerIcalFeedResult(DeleteCalendarOwnerIcalFeedOutcome.FeedNotFound);

        dbContext.CalendarOwnerICalFeeds.Remove(feed);
        await dbContext.SaveChangesAsync(ct);

        return new DeleteCalendarOwnerIcalFeedResult(DeleteCalendarOwnerIcalFeedOutcome.Deleted);
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
