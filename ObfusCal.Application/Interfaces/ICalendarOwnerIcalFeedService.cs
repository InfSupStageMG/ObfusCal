namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerIcalFeedService
{
    Task<IReadOnlyList<CalendarOwnerIcalFeedItem>> ListFeedsAsync(
        Guid calendarOwnerId,
        CancellationToken ct = default);

    Task<AddCalendarOwnerIcalFeedResult> AddFeedAsync(
        Guid calendarOwnerId,
        string feedUrl,
        CancellationToken ct = default);

    Task<DeleteCalendarOwnerIcalFeedResult> DeleteFeedAsync(
        Guid calendarOwnerId,
        Guid feedId,
        CancellationToken ct = default);
}

public sealed record CalendarOwnerIcalFeedItem(Guid Id, string FeedUrl);

public enum AddCalendarOwnerIcalFeedOutcome
{
    Added,
    Duplicate,
    CalendarOwnerNotFound
}

public sealed record AddCalendarOwnerIcalFeedResult(
    AddCalendarOwnerIcalFeedOutcome Outcome,
    Guid? FeedId = null,
    string? FeedUrl = null);

public enum DeleteCalendarOwnerIcalFeedOutcome
{
    Deleted,
    FeedNotFound,
    CalendarOwnerNotFound
}

public sealed record DeleteCalendarOwnerIcalFeedResult(DeleteCalendarOwnerIcalFeedOutcome Outcome);

