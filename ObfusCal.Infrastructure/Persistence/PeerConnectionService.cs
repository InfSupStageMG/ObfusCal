using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class PeerConnectionService(
    AppDbContext dbContext,
    IUrlSafetyValidator urlSafetyValidator) : IPeerConnectionService
{
    public async Task<IReadOnlyList<PeerConnectionSummary>> ListAsync(CancellationToken ct = default)
    {
        return await dbContext.PeerConnections
            .AsNoTracking()
            .Where(p => p.Status == PeerConnectionStatus.Active && p.RevokedAt == null)
            .OrderBy(p => p.InstanceId)
            .Select(p => new PeerConnectionSummary(
                p.Id,
                p.InstanceId,
                p.BaseAddress,
                p.CalendarOwnerMappings.Count))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PeerConnectionRequestSummary>> ListForCalendarOwnerAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        return await dbContext.PeerConnections
            .AsNoTracking()
            .Where(p => p.RequestedByCalendarOwnerId == calendarOwnerId)
            .OrderBy(p => p.ClientOrganisationName)
            .Select(p => new PeerConnectionRequestSummary(
                p.Id,
                p.ClientOrganisationName ?? p.InstanceId,
                p.Status))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdminPeerConnectionSummary>> ListForAdminAsync(CancellationToken ct = default)
    {
        return await dbContext.PeerConnections
            .AsNoTracking()
            .Include(p => p.RequestedByCalendarOwner)
            .OrderBy(p => p.Status)
            .ThenBy(p => p.ClientOrganisationName)
            .ThenBy(p => p.InstanceId)
            .Select(p => new AdminPeerConnectionSummary(
                p.Id,
                p.InstanceId,
                p.BaseAddress,
                p.Status,
                p.ClientOrganisationName,
                p.RequestedByCalendarOwnerId,
                p.RequestedByCalendarOwner == null ? null : p.RequestedByCalendarOwner.Name,
                p.CalendarOwnerMappings.Count,
                p.PinnedCertificateThumbprint,
                p.ClientCertificateThumbprint))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PeerSyncStatus>> ListSyncStatusAsync(CancellationToken ct = default)
    {
        return await dbContext.PeerConnections
            .AsNoTracking()
            .Where(p => p.Status == PeerConnectionStatus.Active && p.RevokedAt == null)
            .OrderBy(p => p.InstanceId)
            .Select(p => new PeerSyncStatus(
                p.InstanceId,
                p.BaseAddress,
                p.LastSyncedAt,
                p.LastSyncSucceeded))
            .ToListAsync(ct);
    }

    public async Task<PeerConnectionSummary> CreateAsync(string instanceId, string baseAddress, string apiKey, IEnumerable<string>? scopes = null, CancellationToken ct = default)
    {
        var createValidation = await urlSafetyValidator.ValidateAsync(baseAddress, ct);
        if (!createValidation.IsValid)
            throw new ArgumentException(createValidation.Message, nameof(baseAddress));

        var normalizedBaseAddress = NormalizeAbsoluteUrl(baseAddress);

        var peer = new PeerConnection
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId.Trim(),
            BaseAddress = normalizedBaseAddress,
            ApiKeyHash = PeerApiKeySecurity.Hash(apiKey.Trim()),
            Scopes = PeerApiScopes.Normalize(scopes ?? PeerApiScopes.DefaultScopes),
            Status = PeerConnectionStatus.Active
        };

        dbContext.PeerConnections.Add(peer);
        await dbContext.SaveChangesAsync(ct);

        return new PeerConnectionSummary(peer.Id, peer.InstanceId, peer.BaseAddress, 0);
    }

    public async Task<CreatePeerConnectionRequestResult> CreateRequestAsync(Guid calendarOwnerId, string clientOrganisationName, CancellationToken ct = default)
    {
        var ownerExists = await dbContext.CalendarOwners.AnyAsync(o => o.Id == calendarOwnerId, ct);
        if (!ownerExists)
            return new CreatePeerConnectionRequestResult(CreatePeerConnectionRequestOutcome.CalendarOwnerNotFound);

        var normalizedOrganisationName = NormalizeClientOrganisationName(clientOrganisationName);

        var alreadyRequested = await dbContext.PeerConnections
            .AnyAsync(p =>
                p.RequestedByCalendarOwnerId == calendarOwnerId
                && p.ClientOrganisationNameNormalized == normalizedOrganisationName,
                ct);

        if (alreadyRequested)
            return new CreatePeerConnectionRequestResult(CreatePeerConnectionRequestOutcome.Duplicate);

        var peer = new PeerConnection
        {
            Id = Guid.NewGuid(),
            InstanceId = $"request-{Guid.NewGuid():N}",
            BaseAddress = "requested",
            ApiKeyHash = string.Empty,
            Scopes = PeerApiScopes.DefaultSerializedScopes,
            Status = PeerConnectionStatus.Requested,
            ClientOrganisationName = clientOrganisationName.Trim(),
            ClientOrganisationNameNormalized = normalizedOrganisationName,
            RequestedByCalendarOwnerId = calendarOwnerId
        };

        dbContext.PeerConnections.Add(peer);
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new CreatePeerConnectionRequestResult(CreatePeerConnectionRequestOutcome.Duplicate);
        }

        return new CreatePeerConnectionRequestResult(CreatePeerConnectionRequestOutcome.Created, peer.Id);
    }

    public async Task<ApprovePeerConnectionResult> ApproveAsync(
        Guid id,
        string peerBaseUrl,
        IEnumerable<string>? scopes = null,
        string? pinnedCertificateThumbprint = null,
        string? clientCertificateThumbprint = null,
        CancellationToken ct = default)
    {
        var validation = await urlSafetyValidator.ValidateAsync(peerBaseUrl, ct);
        if (!validation.IsValid)
            return new ApprovePeerConnectionResult(ApprovePeerConnectionOutcome.InvalidBaseUrl);

        var peer = await dbContext.PeerConnections.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (peer is null)
            return new ApprovePeerConnectionResult(ApprovePeerConnectionOutcome.NotFound);

        if (peer.Status == PeerConnectionStatus.Active)
            return new ApprovePeerConnectionResult(ApprovePeerConnectionOutcome.AlreadyActive);

        var apiKey = PeerApiKeySecurity.GenerateApiKey();

        peer.BaseAddress = NormalizeAbsoluteUrl(peerBaseUrl);
        peer.PinnedCertificateThumbprint = NormalizeThumbprint(pinnedCertificateThumbprint);
        peer.ClientCertificateThumbprint = NormalizeThumbprint(clientCertificateThumbprint);
        peer.ApiKeyHash = PeerApiKeySecurity.Hash(apiKey);
        peer.Scopes = PeerApiScopes.Normalize(scopes ?? PeerApiScopes.DefaultScopes);
        peer.RevokedAt = null;
        peer.Status = PeerConnectionStatus.Active;

        await dbContext.SaveChangesAsync(ct);

        return new ApprovePeerConnectionResult(ApprovePeerConnectionOutcome.Approved, apiKey);
    }

    public async Task<RotatePeerApiKeyResult> RotateApiKeyAsync(Guid id, CancellationToken ct = default)
    {
        var peer = await dbContext.PeerConnections.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (peer is null)
            return new RotatePeerApiKeyResult(RotatePeerApiKeyOutcome.NotFound);

        if (peer.RevokedAt is not null)
            return new RotatePeerApiKeyResult(RotatePeerApiKeyOutcome.Revoked);

        if (peer.Status != PeerConnectionStatus.Active)
            return new RotatePeerApiKeyResult(RotatePeerApiKeyOutcome.NotActive);

        var apiKey = PeerApiKeySecurity.GenerateApiKey();
        peer.ApiKeyHash = PeerApiKeySecurity.Hash(apiKey);
        await dbContext.SaveChangesAsync(ct);

        return new RotatePeerApiKeyResult(RotatePeerApiKeyOutcome.Rotated, apiKey);
    }

    public async Task<RevokePeerConnectionResult> RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var peer = await dbContext.PeerConnections.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (peer is null)
            return new RevokePeerConnectionResult(RevokePeerConnectionOutcome.NotFound);

        if (peer.RevokedAt is not null)
            return new RevokePeerConnectionResult(RevokePeerConnectionOutcome.AlreadyRevoked);

        peer.RevokedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return new RevokePeerConnectionResult(RevokePeerConnectionOutcome.Revoked);
    }

    public async Task<bool> SuspendAsync(Guid id, CancellationToken ct = default)
    {
        var peer = await dbContext.PeerConnections.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (peer is null)
            return false;

        peer.Status = PeerConnectionStatus.Suspended;
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var peer = await dbContext.PeerConnections.FindAsync([id], ct);
        if (peer is null) return false;

        dbContext.PeerConnections.Remove(peer);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<LinkOwnerToPeerResult> LinkOwnerToPeerAsync(Guid calendarOwnerId, Guid peerConnectionId, CancellationToken ct = default)
    {
        var ownerExists = await dbContext.CalendarOwners.AnyAsync(o => o.Id == calendarOwnerId, ct);
        if (!ownerExists) return new LinkOwnerToPeerResult(LinkOwnerToPeerOutcome.OwnerNotFound);

        var peerExists = await dbContext.PeerConnections.AnyAsync(p => p.Id == peerConnectionId, ct);
        if (!peerExists) return new LinkOwnerToPeerResult(LinkOwnerToPeerOutcome.PeerNotFound);

        var alreadyLinked = await dbContext.CalendarOwnerPeerMappings
            .AnyAsync(m => m.CalendarOwnerId == calendarOwnerId && m.PeerConnectionId == peerConnectionId, ct);
        if (alreadyLinked) return new LinkOwnerToPeerResult(LinkOwnerToPeerOutcome.AlreadyLinked);

        var mapping = new CalendarOwnerPeerMapping
        {
            Id = Guid.NewGuid(),
            CalendarOwnerId = calendarOwnerId,
            PeerConnectionId = peerConnectionId,
            CalendarOwnerRef = Guid.NewGuid()
        };

        dbContext.CalendarOwnerPeerMappings.Add(mapping);
        await dbContext.SaveChangesAsync(ct);

        return new LinkOwnerToPeerResult(LinkOwnerToPeerOutcome.Linked, mapping.CalendarOwnerRef);
    }

    private static string NormalizeClientOrganisationName(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeThumbprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeAbsoluteUrl(string url) =>
        new Uri(url.Trim(), UriKind.Absolute).AbsoluteUri.TrimEnd('/');
}

