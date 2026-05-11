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

        // Stream candidate peer connections from the database without materializing all rows at once.
        // Iterate through each candidate and verify the API key, stopping on the first match.
        await foreach (var candidate in dbContext.PeerConnections
            .AsNoTracking()
            .Where(connection => connection.Status == PeerConnectionStatus.Active && connection.RevokedAt == null)
            .Select(connection => new
            {
                connection.Id,
                connection.InstanceId,
                connection.Scopes,
                connection.ApiKeyHash
            })
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            if (PeerApiKeySecurity.Verify(providedApiKey, candidate.ApiKeyHash))
            {
                var scopes = candidate.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return new PeerApiKeyAuthenticationResult(candidate.Id, candidate.InstanceId, scopes);
            }
        }

        return null;
    }
}



