using Microsoft.Extensions.Logging.Abstractions;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Application.UseCases.GetBusySlots;
using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation;

namespace ObfusCal.Tests.Unit.UseCases;

[TestClass]
public class GetBusySlotsQueryHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTimeOffset From = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Handle_ReturnsObfuscatedBusySlots()
    {
        var events = new CalendarEvent[]
        {
            new("evt-1", "Secret Meeting", "Private desc",
                From.AddHours(9), From.AddHours(10), ["alice@example.com"], "Room A")
        };

        var handler = CreateHandler(events);
        var result = await handler.ExecuteAsync(new GetBusySlotsQuery(OwnerId, From, To), CancellationToken.None);

        Assert.HasCount(1, result);
        Assert.AreEqual(From.AddHours(9), result[0].Start);
        Assert.AreEqual(From.AddHours(10), result[0].End);
    }

    [TestMethod]
    public async Task Handle_WithNoEvents_ReturnsEmptyList()
    {
        var handler = CreateHandler([]);
        var result = await handler.ExecuteAsync(new GetBusySlotsQuery(OwnerId, From, To), CancellationToken.None);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task Handle_MapsAllFieldsToResponse()
    {
        var events = new CalendarEvent[]
        {
            new("evt-1", "Title", "Desc",
                From.AddHours(9), From.AddHours(10), ["a@b.com"], "Loc")
        };

        var handler = CreateHandler(events);
        var result = await handler.ExecuteAsync(new GetBusySlotsQuery(OwnerId, From, To), CancellationToken.None);

        Assert.AreEqual("Title", result[0].Title);
        Assert.AreEqual("Desc", result[0].Description);
        Assert.IsNotNull(result[0].AttendeeEmails);
        Assert.AreEqual("a@b.com", result[0].AttendeeEmails![0]);
        Assert.AreEqual("Loc", result[0].Location);
    }

    [TestMethod]
    public async Task Handle_UsesClientContext()
    {
        var profileService = new FakeObfuscationProfileService();
        var handler = CreateHandler([], profileService);

        await handler.ExecuteAsync(new GetBusySlotsQuery(OwnerId, From, To), CancellationToken.None);

        Assert.AreEqual(ObfuscationAuditContext.Client, profileService.LastRequestedContext,
            "Busy slots should use Client context");
    }

    [TestMethod]
    public async Task Handle_WithMultipleEvents_ReturnsOneResponsePerEvent()
    {
        var events = Enumerable.Range(0, 3)
            .Select(i => new CalendarEvent($"evt-{i}", "Meeting", null,
                From.AddHours(9 + i * 2), From.AddHours(10 + i * 2), [], null))
            .ToArray();

        var handler = CreateHandler(events);
        var result = await handler.ExecuteAsync(new GetBusySlotsQuery(OwnerId, From, To), CancellationToken.None);

        Assert.HasCount(3, result);
    }

    private static GetBusySlotsUseCase CreateHandler(
        IReadOnlyList<CalendarEvent> events,
        FakeObfuscationProfileService? profileService = null)
    {
        var calendarSource = new FakeCalendarSource(events);
        profileService ??= new FakeObfuscationProfileService();

        var pipeline = new ObfuscationPipeline(
            Array.Empty<IObfuscationTransformer>(),
            Array.Empty<IBusySlotTransformer>(),
            NullLogger<ObfuscationPipeline>.Instance);

        return new GetBusySlotsUseCase(
            new FixedCalendarSourceResolver(calendarSource), pipeline, profileService,
            NullLogger<GetBusySlotsUseCase>.Instance);
    }

    private sealed class FixedCalendarSourceResolver(ICalendarSource source) : ICalendarSourceResolver
    {
        public Task<ICalendarSource> ResolveAsync(Guid? calendarOwnerId = null, CancellationToken ct = default) =>
            Task.FromResult(source);
    }

    private sealed class FakeCalendarSource(IReadOnlyList<CalendarEvent> events) : ICalendarSource
    {
        public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
            DateTimeOffset from, DateTimeOffset to, Guid? calendarOwnerId = null, CancellationToken ct = default)
            => Task.FromResult(events);
    }

    private sealed class FakeObfuscationProfileService : ICalendarOwnerObfuscationProfileService
    {
        public ObfuscationAuditContext? LastRequestedContext { get; private set; }

        public Task<IReadOnlyList<ObfuscationProfileSettings>> GetProfilesAsync(Guid calendarOwnerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ObfuscationProfileSettings>>([]);

        public Task<ObfuscationProfileSettings> GetProfileAsync(Guid calendarOwnerId, ObfuscationAuditContext context, CancellationToken ct = default)
        {
            LastRequestedContext = context;
            return Task.FromResult(ObfuscationProfileSettings.CreateDefault(context));
        }

        public Task<ObfuscationProfileSettings> SetProfileAsync(Guid calendarOwnerId, ObfuscationProfileSettings profile, CancellationToken ct = default)
            => Task.FromResult(profile);
    }
}

