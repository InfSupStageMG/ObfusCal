namespace ObfusCal.Application.Interfaces;

/// <summary>
/// Resolves calendar owner IDs that a peer is authorized to access
/// based on active, non-revoked peer connection mappings.
/// </summary>
public interface IPeerCalendarOwnerResolver
{
    Task<IReadOnlyList<Guid>> ResolveCalendarOwnerIdsAsync(string peerId, Guid calendarOwnerRef, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> ResolveAllCalendarOwnerIdsAsync(string peerId, CancellationToken ct = default);
    Task<Guid> ResolveSingleCalendarOwnerIdAsync(string peerId, Guid calendarOwnerRef, CancellationToken ct = default);
}

