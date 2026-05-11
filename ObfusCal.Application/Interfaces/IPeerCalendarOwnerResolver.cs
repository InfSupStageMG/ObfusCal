namespace ObfusCal.Application.Interfaces;

/// <summary>
/// Resolves calendar owner IDs that a peer is authorized to access
/// based on active, non-revoked peer connection mappings.
/// </summary>
public interface IPeerCalendarOwnerResolver
{
    /// <summary>
    /// Returns the calendar owner IDs mapped to the given peer for a specific calendar owner ref.
    /// </summary>
    Task<IReadOnlyList<Guid>> ResolveCalendarOwnerIdsAsync(string peerId, Guid calendarOwnerRef, CancellationToken ct = default);

    /// <summary>
    /// Returns all distinct calendar owner IDs mapped to the given peer.
    /// </summary>
    Task<IReadOnlyList<Guid>> ResolveAllCalendarOwnerIdsAsync(string peerId, CancellationToken ct = default);

    /// <summary>
    /// Returns the calendar owner ID for a specific calendar owner ref accessible by the given peer,
    /// or <see cref="Guid.Empty"/> if no mapping exists.
    /// </summary>
    Task<Guid> ResolveSingleCalendarOwnerIdAsync(string peerId, Guid calendarOwnerRef, CancellationToken ct = default);
}

