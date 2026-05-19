using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Persistence;

[TestClass]
public class CalendarOwnerIcalFeedServiceTests
{
    private static (CalendarOwnerIcalFeedService svc, AppDbContext db, Guid ownerId) Setup()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Test" });
        db.SaveChanges();
        return (new CalendarOwnerIcalFeedService(db, new PermissiveUrlSafetyValidator()), db, ownerId);
    }

    private static (CalendarOwnerIcalFeedService svc, Guid ownerId) SetupRejectingUrls()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Test" });
        db.SaveChanges();
        return (new CalendarOwnerIcalFeedService(db, new RejectingUrlSafetyValidator()), ownerId);
    }

    [TestMethod]
    public async Task AddFeedAsync_ReturnsCalendarOwnerNotFound_WhenOwnerMissing()
    {
        var (svc, _, _) = Setup();
        var result = await svc.AddFeedAsync(Guid.NewGuid(), "https://example.com/feed.ics");
        Assert.AreEqual(AddCalendarOwnerIcalFeedOutcome.CalendarOwnerNotFound, result.Outcome);
    }

    [TestMethod]
    public async Task AddFeedAsync_ReturnsAdded_OnSuccess()
    {
        var (svc, _, ownerId) = Setup();
        var result = await svc.AddFeedAsync(ownerId, "https://example.com/feed.ics");

        Assert.AreEqual(AddCalendarOwnerIcalFeedOutcome.Added, result.Outcome);
        Assert.IsNotNull(result.FeedId);
        Assert.AreEqual("https://example.com/feed.ics", result.FeedUrl);
    }

    [TestMethod]
    public async Task AddFeedAsync_ReturnsDuplicate_WhenSameUrlAddedTwice()
    {
        var (svc, _, ownerId) = Setup();
        await svc.AddFeedAsync(ownerId, "https://example.com/feed.ics");
        var result = await svc.AddFeedAsync(ownerId, "https://example.com/feed.ics");

        Assert.AreEqual(AddCalendarOwnerIcalFeedOutcome.Duplicate, result.Outcome);
    }

    [TestMethod]
    public async Task ListFeedsAsync_ReturnsEmpty_WhenNoFeeds()
    {
        var (svc, _, ownerId) = Setup();
        var feeds = await svc.ListFeedsAsync(ownerId);
        Assert.IsEmpty(feeds);
    }

    [TestMethod]
    public async Task ListFeedsAsync_ReturnsAddedFeeds()
    {
        var (svc, _, ownerId) = Setup();
        await svc.AddFeedAsync(ownerId, "https://a.com/a.ics");
        await svc.AddFeedAsync(ownerId, "https://b.com/b.ics");

        var feeds = await svc.ListFeedsAsync(ownerId);

        Assert.HasCount(2, feeds);
    }

    [TestMethod]
    public async Task ListFeedsAsync_DoesNotReturnOtherOwnersFeeds()
    {
        var (svc, db, ownerA) = Setup();
        var ownerB = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerB, Name = "Other" });
        db.SaveChanges();

        await svc.AddFeedAsync(ownerA, "https://a.com/feed.ics");
        await svc.AddFeedAsync(ownerB, "https://b.com/feed.ics");

        var feedsA = await svc.ListFeedsAsync(ownerA);
        var feedsB = await svc.ListFeedsAsync(ownerB);

        Assert.HasCount(1, feedsA);
        Assert.HasCount(1, feedsB);
        Assert.AreEqual("https://a.com/feed.ics", feedsA[0].FeedUrl);
    }

    [TestMethod]
    public async Task DeleteFeedAsync_ReturnsCalendarOwnerNotFound_WhenOwnerMissing()
    {
        var (svc, _, _) = Setup();
        var result = await svc.DeleteFeedAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.AreEqual(DeleteCalendarOwnerIcalFeedOutcome.CalendarOwnerNotFound, result.Outcome);
    }

    [TestMethod]
    public async Task DeleteFeedAsync_ReturnsFeedNotFound_WhenFeedDoesNotExist()
    {
        var (svc, _, ownerId) = Setup();
        var result = await svc.DeleteFeedAsync(ownerId, Guid.NewGuid());
        Assert.AreEqual(DeleteCalendarOwnerIcalFeedOutcome.FeedNotFound, result.Outcome);
    }

    [TestMethod]
    public async Task DeleteFeedAsync_ReturnsDeleted_OnSuccess()
    {
        var (svc, _, ownerId) = Setup();
        var addResult = await svc.AddFeedAsync(ownerId, "https://example.com/feed.ics");
        var result = await svc.DeleteFeedAsync(ownerId, addResult.FeedId!.Value);

        Assert.AreEqual(DeleteCalendarOwnerIcalFeedOutcome.Deleted, result.Outcome);
        var remaining = await svc.ListFeedsAsync(ownerId);
        Assert.IsEmpty(remaining);
    }

    [TestMethod]
    public async Task DeleteFeedAsync_DoesNotDeleteOtherOwnersFeed()
    {
        var (svc, db, ownerA) = Setup();
        var ownerB = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerB, Name = "Other" });
        db.SaveChanges();

        var feedA = await svc.AddFeedAsync(ownerA, "https://a.com/feed.ics");

        // Try deleting ownerA's feed using ownerB's id → FeedNotFound
        var result = await svc.DeleteFeedAsync(ownerB, feedA.FeedId!.Value);
        Assert.AreEqual(DeleteCalendarOwnerIcalFeedOutcome.FeedNotFound, result.Outcome);
    }

    [TestMethod]
    public async Task AddFeedAsync_ReturnsNotFound_WhenDifferentOwnerIdProvided()
    {
        var (svc, _, _) = Setup();
        var unknownOwnerId = Guid.NewGuid();

        var result = await svc.AddFeedAsync(unknownOwnerId, "https://example.com/feed.ics");

        Assert.AreEqual(AddCalendarOwnerIcalFeedOutcome.CalendarOwnerNotFound, result.Outcome);
    }

    [TestMethod]
    public async Task AddFeedAsync_ReturnsInvalidUrl_WhenUrlValidatorRejectsInput()
    {
        var (svc, ownerId) = SetupRejectingUrls();

        var result = await svc.AddFeedAsync(ownerId, "https://127.0.0.1/feed.ics");

        Assert.AreEqual(AddCalendarOwnerIcalFeedOutcome.InvalidUrl, result.Outcome);
    }

    [TestMethod]
    public async Task DeleteFeedAsync_ReturnsNotFound_WhenDifferentOwnerIdProvided()
    {
        var (svc, _, _) = Setup();
        var unknownOwnerId = Guid.NewGuid();

        var result = await svc.DeleteFeedAsync(unknownOwnerId, Guid.NewGuid());

        Assert.AreEqual(DeleteCalendarOwnerIcalFeedOutcome.CalendarOwnerNotFound, result.Outcome);
    }

    [TestMethod]
    public async Task DeleteFeedAsync_RequiresBothFeedIdAndOwnerIdToMatch()
    {
        var (svc, db, ownerA) = Setup();
        var ownerB = Guid.NewGuid();
        db.CalendarOwners.Add(new CalendarOwner { Id = ownerB, Name = "B" });
        db.SaveChanges();

        var feedA = await svc.AddFeedAsync(ownerA, "https://a.com/feed.ics");
        var feedB = await svc.AddFeedAsync(ownerB, "https://b.com/feed.ics");

        // Try deleting feedA using ownerB - should fail
        var result1 = await svc.DeleteFeedAsync(ownerB, feedA.FeedId!.Value);
        Assert.AreEqual(DeleteCalendarOwnerIcalFeedOutcome.FeedNotFound, result1.Outcome);

        // Try deleting feedB using ownerA - should fail
        var result2 = await svc.DeleteFeedAsync(ownerA, feedB.FeedId!.Value);
        Assert.AreEqual(DeleteCalendarOwnerIcalFeedOutcome.FeedNotFound, result2.Outcome);

        // Correct combination should work
        var result3 = await svc.DeleteFeedAsync(ownerA, feedA.FeedId!.Value);
        Assert.AreEqual(DeleteCalendarOwnerIcalFeedOutcome.Deleted, result3.Outcome);
    }

    private sealed class PermissiveUrlSafetyValidator : IUrlSafetyValidator
    {
        public Task<UrlSafetyValidationResult> ValidateAsync(string url, CancellationToken ct = default) =>
            Task.FromResult(UrlSafetyValidationResult.Success());

        public Task<UrlSafetyValidationResult> ValidateAsync(Uri uri, CancellationToken ct = default) =>
            Task.FromResult(UrlSafetyValidationResult.Success());
    }

    private sealed class RejectingUrlSafetyValidator : IUrlSafetyValidator
    {
        public Task<UrlSafetyValidationResult> ValidateAsync(string url, CancellationToken ct = default) =>
            Task.FromResult(UrlSafetyValidationResult.Fail(
                UrlSafetyValidationError.PrivateNetworkHost,
                "Private hosts are blocked."));

        public Task<UrlSafetyValidationResult> ValidateAsync(Uri uri, CancellationToken ct = default) =>
            Task.FromResult(UrlSafetyValidationResult.Fail(
                UrlSafetyValidationError.PrivateNetworkHost,
                "Private hosts are blocked."));
    }
}

