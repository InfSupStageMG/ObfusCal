using System.Net.Http.Json;
using ObfusCal.Core.Interfaces;
using ObfusCal.Core.Models;
using Microsoft.Extensions.Logging;

namespace ObfusCal.Infrastructure.Peers;

public class HttpPeerClient(HttpClient http, ILogger<HttpPeerClient> logger) : IPeerClient
{
    public async Task<IReadOnlyList<BusySlot>> PullSlotsAsync(
        PeerInfo peer, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var url = $"{peer.BaseUrl.TrimEnd('/')}/api/sync/pull" +
                  $"?from={Uri.EscapeDataString(from.ToString("O"))}" +
                  $"&to={Uri.EscapeDataString(to.ToString("O"))}";

        logger.LogInformation("Pulling slots from peer {PeerId} at {Url}", peer.Id, url);

        var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var slots = await response.Content.ReadFromJsonAsync<List<BusySlot>>(ct) ?? [];
        logger.LogInformation("Received {Count} slots from peer {PeerId}", slots.Count, peer.Id);
        return slots;
    }

    public async Task PushSlotsAsync(
        PeerInfo peer, IReadOnlyList<BusySlot> slots, CancellationToken ct = default)
    {
        var url = $"{peer.BaseUrl.TrimEnd('/')}/api/sync/push";

        logger.LogInformation("Pushing {Count} slots to peer {PeerId}", slots.Count, peer.Id);

        var response = await http.PostAsJsonAsync(url, slots, ct);
        response.EnsureSuccessStatusCode();
    }
}