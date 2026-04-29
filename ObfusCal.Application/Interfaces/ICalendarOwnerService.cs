namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerService
{
    Task<IReadOnlyList<CalendarOwnerSummary>> ListAsync(CancellationToken ct = default);
    Task<CalendarOwnerInfo?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CalendarOwnerSummary> CreateAsync(string name, string? entraObjectId = null, CancellationToken ct = default);
}

public sealed record CalendarOwnerSummary(Guid Id, string Name, bool HasGraphConsent, int FeedCount, int PeerMappingCount);
public sealed record CalendarOwnerInfo(Guid Id, string Name, bool HasGraphConsent);



