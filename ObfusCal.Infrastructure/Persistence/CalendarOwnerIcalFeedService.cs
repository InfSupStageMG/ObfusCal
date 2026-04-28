using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class CalendarOwnerIcalFeedService(AppDbContext dbContext) : ICalendarOwnerIcalFeedService
{
    public async Task<IReadOnlyList<CalendarOwnerIcalFeedItem>> ListFeedsAsync(
        Guid calendarOwnerId,
        CancellationToken ct = default)
    {
        return await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .Where(feed => feed.CalendarOwnerId == calendarOwnerId)
            .OrderBy(feed => feed.FeedUrl)
            .Select(feed => new CalendarOwnerIcalFeedItem(feed.Id, feed.FeedUrl))
            .ToListAsync(ct);
    }

    public async Task<AddCalendarOwnerIcalFeedResult> AddFeedAsync(
        Guid calendarOwnerId,
        string feedUrl,
        CancellationToken ct = default)
    {
        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(owner => owner.Id == calendarOwnerId, ct);

        if (!ownerExists)
            return new AddCalendarOwnerIcalFeedResult(AddCalendarOwnerIcalFeedOutcome.CalendarOwnerNotFound);

        var normalizedFeedUrl = feedUrl.Trim();

        var alreadyExists = await dbContext.CalendarOwnerICalFeeds
            .AsNoTracking()
            .AnyAsync(feed => feed.CalendarOwnerId == calendarOwnerId && feed.FeedUrl == normalizedFeedUrl, ct);

        if (alreadyExists)
            return new AddCalendarOwnerIcalFeedResult(AddCalendarOwnerIcalFeedOutcome.Duplicate);

        var feed = new CalendarOwnerICalFeed
        {
            Id = Guid.NewGuid(),
            CalendarOwnerId = calendarOwnerId,
            FeedUrl = normalizedFeedUrl
        };

        dbContext.CalendarOwnerICalFeeds.Add(feed);
        await dbContext.SaveChangesAsync(ct);

        return new AddCalendarOwnerIcalFeedResult(
            AddCalendarOwnerIcalFeedOutcome.Added,
            feed.Id,
            feed.FeedUrl);
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

        var feed = await dbContext.CalendarOwnerICalFeeds
            .SingleOrDefaultAsync(x => x.Id == feedId && x.CalendarOwnerId == calendarOwnerId, ct);

        if (feed is null)
            return new DeleteCalendarOwnerIcalFeedResult(DeleteCalendarOwnerIcalFeedOutcome.FeedNotFound);

        dbContext.CalendarOwnerICalFeeds.Remove(feed);
        await dbContext.SaveChangesAsync(ct);

        return new DeleteCalendarOwnerIcalFeedResult(DeleteCalendarOwnerIcalFeedOutcome.Deleted);
    }
}
