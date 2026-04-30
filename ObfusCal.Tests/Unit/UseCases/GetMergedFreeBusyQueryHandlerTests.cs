using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Application.UseCases.GetMergedFreeBusy;
using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation;

namespace ObfusCal.Tests.Unit.UseCases;

[TestClass]
public class GetMergedFreeBusyQueryHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Handle_CombinesOwnAndShadowSlots_InSortedOrder()
    {
        var ownEvent = new CalendarEvent("own-1", "Meeting", null,
            From.AddHours(10), From.AddHours(11), [], null);
        var shadowSlot = new BusySlot("shadow-1", From.AddHours(8), From.AddHours(9));

        var calendarSource = new FakeCalendarSource([ownEvent]);
        var shadowStore = new FakeShadowSlotStore([shadowSlot]);
        var profileService = new FakeObfuscationProfileService();

        var pipeline = new ObfuscationPipeline(
            Array.Empty<IObfuscationTransformer>(),
            Array.Empty<IBusySlotTransformer>(),
            NullLogger<ObfuscationPipeline>.Instance);

        var handler = new GetMergedFreeBusyUseCase(
            calendarSource, pipeline, shadowStore, profileService,
            NullLogger<GetMergedFreeBusyUseCase>.Instance);

        var query = new GetMergedFreeBusyQuery(OwnerId, From, To);
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        Assert.IsTrue(result.Count >= 2, "Should contain both own and shadow slots");
        Assert.IsTrue(result[0].Start <= result[1].Start, "Results should be sorted by Start");
    }

    [TestMethod]
    public async Task Handle_ReturnsOwnSlotsWhenNoShadowSlots()
    {
        var ownEvent = new CalendarEvent("own-1", "Meeting", "desc",
            From.AddHours(10), From.AddHours(11), ["alice@example.com"], "Room 1");
        var calendarSource = new FakeCalendarSource([ownEvent]);
        var shadowStore = new FakeShadowSlotStore([]);
        var profileService = new FakeObfuscationProfileService();

        var pipeline = new ObfuscationPipeline(
            Array.Empty<IObfuscationTransformer>(),
            Array.Empty<IBusySlotTransformer>(),
            NullLogger<ObfuscationPipeline>.Instance);

        var handler = new GetMergedFreeBusyUseCase(
            calendarSource, pipeline, shadowStore, profileService,
            NullLogger<GetMergedFreeBusyUseCase>.Instance);

        var result = await handler.ExecuteAsync(new GetMergedFreeBusyQuery(OwnerId, From, To), CancellationToken.None);

        Assert.HasCount(1, result);
        Assert.AreEqual(From.AddHours(10), result[0].Start);
        Assert.AreEqual(From.AddHours(11), result[0].End);
    }

    [TestMethod]
    public async Task Handle_ReturnsShadowSlotsWhenNoOwnEvents()
    {
        var shadowSlot = new BusySlot("shadow-1", From.AddHours(8), From.AddHours(9));
        var calendarSource = new FakeCalendarSource([]);
        var shadowStore = new FakeShadowSlotStore([shadowSlot]);
        var profileService = new FakeObfuscationProfileService();

        var pipeline = new ObfuscationPipeline(
            Array.Empty<IObfuscationTransformer>(),
            Array.Empty<IBusySlotTransformer>(),
            NullLogger<ObfuscationPipeline>.Instance);

        var handler = new GetMergedFreeBusyUseCase(
            calendarSource, pipeline, shadowStore, profileService,
            NullLogger<GetMergedFreeBusyUseCase>.Instance);

        var result = await handler.ExecuteAsync(new GetMergedFreeBusyQuery(OwnerId, From, To), CancellationToken.None);

        Assert.HasCount(1, result);
        Assert.AreEqual(From.AddHours(8), result[0].Start);
    }

    [TestMethod]
    public async Task Handle_MapsAllFieldsToResponse()
    {
        var ownEvent = new CalendarEvent("own-1", "Title", "Desc",
            From.AddHours(10), From.AddHours(11), ["a@b.com"], "Room");
        var calendarSource = new FakeCalendarSource([ownEvent]);
        var shadowStore = new FakeShadowSlotStore([]);
        var profileService = new FakeObfuscationProfileService();

        // No transformers so fields pass through
        var pipeline = new ObfuscationPipeline(
            Array.Empty<IObfuscationTransformer>(),
            Array.Empty<IBusySlotTransformer>(),
            NullLogger<ObfuscationPipeline>.Instance);

        var handler = new GetMergedFreeBusyUseCase(
            calendarSource, pipeline, shadowStore, profileService,
            NullLogger<GetMergedFreeBusyUseCase>.Instance);

        var result = await handler.ExecuteAsync(new GetMergedFreeBusyQuery(OwnerId, From, To), CancellationToken.None);

        Assert.HasCount(1, result);
        Assert.AreEqual("Title", result[0].Title);
        Assert.AreEqual("Desc", result[0].Description);
        Assert.IsNotNull(result[0].AttendeeEmails);
        Assert.AreEqual("a@b.com", result[0].AttendeeEmails![0]);
        Assert.AreEqual("Room", result[0].Location);
    }

    [TestMethod]
    public async Task Handle_UsesInternalContext_ForObfuscation()
    {
        var ownEvent = new CalendarEvent("own-1", "Meeting", null,
            From.AddHours(10), From.AddHours(11), [], null);
        var calendarSource = new FakeCalendarSource([ownEvent]);
        var shadowStore = new FakeShadowSlotStore([]);
        var profileService = new FakeObfuscationProfileService();

        var pipeline = new ObfuscationPipeline(
            Array.Empty<IObfuscationTransformer>(),
            Array.Empty<IBusySlotTransformer>(),
            NullLogger<ObfuscationPipeline>.Instance);

        var handler = new GetMergedFreeBusyUseCase(
            calendarSource, pipeline, shadowStore, profileService,
            NullLogger<GetMergedFreeBusyUseCase>.Instance);

        await handler.ExecuteAsync(new GetMergedFreeBusyQuery(OwnerId, From, To), CancellationToken.None);

        Assert.AreEqual(ObfuscationAuditContext.Internal, profileService.LastRequestedContext,
            "Merged free/busy should use Internal context");
    }

    [TestMethod]
    public async Task Handle_SortsCombinedSlots_ShadowBeforeOwn()
    {
        // Shadow slot is at 8 AM, own is at 10 AM → shadow should come first
        var ownEvent = new CalendarEvent("own-1", "Meeting", null,
            From.AddHours(10), From.AddHours(11), [], null);
        var shadowSlot = new BusySlot("shadow-1", From.AddHours(8), From.AddHours(9));

        var calendarSource = new FakeCalendarSource([ownEvent]);
        var shadowStore = new FakeShadowSlotStore([shadowSlot]);
        var profileService = new FakeObfuscationProfileService();

        var pipeline = new ObfuscationPipeline(
            Array.Empty<IObfuscationTransformer>(),
            Array.Empty<IBusySlotTransformer>(),
            NullLogger<ObfuscationPipeline>.Instance);

        var handler = new GetMergedFreeBusyUseCase(
            calendarSource, pipeline, shadowStore, profileService,
            NullLogger<GetMergedFreeBusyUseCase>.Instance);

        var result = await handler.ExecuteAsync(new GetMergedFreeBusyQuery(OwnerId, From, To), CancellationToken.None);

        Assert.AreEqual(From.AddHours(8), result[0].Start, "Shadow slot (8 AM) should be first");
        Assert.AreEqual(From.AddHours(10), result[1].Start, "Own slot (10 AM) should be second");
    }

    // ---- Fakes ----

    private sealed class FakeCalendarSource(IReadOnlyList<CalendarEvent> events) : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from, DateTimeOffset to, Guid? calendarOwnerId = null, CancellationToken ct = default)
            => Task.FromResult(events);
    }

    private sealed class FakeShadowSlotStore(IReadOnlyList<BusySlot> allSlots) : IShadowSlotStore
    {
        public Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetSlotsAsync(string peerId, Guid calendarOwnerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BusySlot>>([]);
        public Task<IReadOnlyList<BusySlot>> GetSlotsAsync(string peerId, Guid calendarOwnerId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BusySlot>>([]);
        public Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) => Task.FromResult(allSlots);
        public Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(Guid calendarOwnerId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) => Task.FromResult(allSlots);
    }

    private sealed class FakeObfuscationProfileService : ICalendarOwnerObfuscationProfileService
    {
        public ObfuscationAuditContext? LastRequestedContext { get; private set; }

        public Task<IReadOnlyList<ObfuscationProfileSettings>> GetProfilesAsync(Guid calendarOwnerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ObfuscationProfileSettings>>([ObfuscationProfileSettings.CreateDefault(ObfuscationAuditContext.Internal)]);

        public Task<ObfuscationProfileSettings> GetProfileAsync(Guid calendarOwnerId, ObfuscationAuditContext context, CancellationToken ct = default)
        {
            LastRequestedContext = context;
            return Task.FromResult(ObfuscationProfileSettings.CreateDefault(context));
        }

        public Task<ObfuscationProfileSettings> SetProfileAsync(Guid calendarOwnerId, ObfuscationProfileSettings profile, CancellationToken ct = default)
            => Task.FromResult(profile);
    }
}

