using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Security;

public sealed class EfCorePeerApiKeyAuthenticator(AppDbContext dbContext) : IPeerApiKeyAuthenticator
{
    public async Task<PeerApiKeyAuthenticationResult?> AuthenticateAsync(string providedApiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providedApiKey))
            return null;

        var candidates = await dbContext.PeerConnections
            .AsNoTracking()
            .Where(connection => connection.Status == PeerConnectionStatus.Active && connection.RevokedAt == null)
            .Select(connection => new
            {
                connection.Id,
                connection.InstanceId,
                connection.Scopes,
                connection.ApiKeyHash
            })
            .ToListAsync(ct);

        var peer = candidates.FirstOrDefault(connection => PeerApiKeySecurity.Verify(providedApiKey, connection.ApiKeyHash));
        if (peer is null)
            return null;

        var scopes = peer.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new PeerApiKeyAuthenticationResult(peer.Id, peer.InstanceId, scopes);
    }
}

