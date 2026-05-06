using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using CoreBusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Infrastructure.Sync;

public sealed class InboundPeerPullSyncService(
    AppDbContext dbContext,
    IShadowSlotStore shadowSlotStore,
    IHttpClientFactory httpClientFactory,
    ISecretProvider secretProvider,
    IOptions<SyncOptions> syncOptions,
    ILogger<InboundPeerPullSyncService> logger)
    : IInboundPeerPullSyncService
{
    private const string PeerIdHeaderName = "X-Peer-Id";
    private const string PeerApiKeyScheme = "ApiKey";
    private const string BusySlotsRelativePath = "api/sync/busy-slots";

    public async Task RunSyncCycleAsync(CancellationToken ct = default)
    {
        var options = syncOptions.Value;
        var instanceId = secretProvider.GetSecret(SecretKeys.SyncInstanceId) ?? options.InstanceId;
        var apiKey = secretProvider.GetSecret(SecretKeys.SyncApiKey) ?? options.ApiKey;

        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogDebug(
                "Skipping inbound peer pull sync because Sync.InstanceId or Sync.ApiKey is not configured.");
            return;
        }

        var syncWindowStart = DateTimeOffset.UtcNow;
        var syncWindowEnd = syncWindowStart.AddDays(Math.Max(1, options.LookAheadDays));

        var mappings = await dbContext.CalendarOwnerPeerMappings
            .AsNoTracking()
            .Where(mapping => mapping.PeerConnection.Status == PeerConnectionStatus.Active)
            .Select(mapping => new PeerPullTarget(
                mapping.PeerConnectionId,
                mapping.CalendarOwnerId,
                mapping.CalendarOwnerRef,
                mapping.PeerConnection.InstanceId,
                mapping.PeerConnection.BaseAddress))
            .ToListAsync(ct);

        foreach (var mapping in mappings)
        {
            try
            {
                await PullFromPeerAsync(mapping, syncWindowStart, syncWindowEnd, instanceId, apiKey, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Inbound peer pull failed for peer {PeerId} and calendar owner {CalendarOwnerId}; continuing sync cycle.",
                    mapping.PeerInstanceId,
                    mapping.CalendarOwnerId);
                await RecordSyncResultAsync(mapping.PeerConnectionId, succeeded: false);
            }
        }
    }

    private async Task PullFromPeerAsync(
        PeerPullTarget mapping,
        DateTimeOffset from,
        DateTimeOffset to,
        string instanceId,
        string apiKey,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(mapping.BaseAddress, UriKind.Absolute, out var baseUri))
        {
            logger.LogWarning(
                "Skipping inbound peer pull for peer {PeerId} because BaseAddress is invalid.",
                mapping.PeerInstanceId);
            await RecordSyncResultAsync(mapping.PeerConnectionId, succeeded: false);
            return;
        }

        var endpoint = BuildPullUri(baseUri, mapping.CalendarOwnerRef, from, to);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(PeerApiKeyScheme, apiKey);
        request.Headers.Add(PeerIdHeaderName, instanceId);

        var client = httpClientFactory.CreateClient(nameof(InboundPeerPullSyncService));

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var failureReason = $"HTTP {(int)response.StatusCode}";
                logger.LogWarning(
                    "Peer pull failed for peer {PeerId} and calendar owner {CalendarOwnerId} with failure reason {FailureReason}; keeping previous slots.",
                    mapping.PeerInstanceId,
                    mapping.CalendarOwnerId,
                    failureReason);
                await RecordSyncResultAsync(mapping.PeerConnectionId, succeeded: false);
                return;
            }

            var payload = await response.Content.ReadFromJsonAsync<IReadOnlyList<PulledBusySlot>>(cancellationToken: ct) ?? [];
            var pulledSlots = payload
                .Select((slot, index) => new CoreBusySlot(
                    $"{mapping.PeerInstanceId}:{mapping.CalendarOwnerRef}:{index}",
                    slot.Start,
                    slot.End))
                .ToArray();

            await shadowSlotStore.SetSlotsAsync(mapping.PeerInstanceId, mapping.CalendarOwnerId, pulledSlots, ct);

            logger.LogInformation(
                "Successfully pulled {BusySlotCount} slot(s) from peer {PeerId} for calendar owner {CalendarOwnerId}.",
                pulledSlots.Length,
                mapping.PeerInstanceId,
                mapping.CalendarOwnerId);
            await RecordSyncResultAsync(mapping.PeerConnectionId, succeeded: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Peer pull failed for peer {PeerId} and calendar owner {CalendarOwnerId} with failure reason {FailureReason}; keeping previous slots.",
                mapping.PeerInstanceId,
                mapping.CalendarOwnerId,
                ex.Message);
            await RecordSyncResultAsync(mapping.PeerConnectionId, succeeded: false);
        }
    }

    private async Task RecordSyncResultAsync(Guid peerConnectionId, bool succeeded)
    {
        try
        {
            var peer = await dbContext.PeerConnections.FindAsync([peerConnectionId]);
            if (peer is not null)
            {
                peer.LastSyncedAt = DateTimeOffset.UtcNow;
                peer.LastSyncSucceeded = succeeded;
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to persist sync metadata for peer connection {PeerConnectionId}.",
                peerConnectionId);
        }
    }

    private static Uri BuildPullUri(Uri baseUri, Guid calendarOwnerRef, DateTimeOffset from, DateTimeOffset to)
    {
        var fromEncoded = Uri.EscapeDataString(from.ToString("O"));
        var toEncoded = Uri.EscapeDataString(to.ToString("O"));
        var relative = $"{BusySlotsRelativePath}/{calendarOwnerRef}?from={fromEncoded}&to={toEncoded}";
        return new Uri(baseUri, relative);
    }

    private sealed record PeerPullTarget(
        Guid PeerConnectionId,
        Guid CalendarOwnerId,
        Guid CalendarOwnerRef,
        string PeerInstanceId,
        string BaseAddress);

    private sealed record PulledBusySlot(DateTimeOffset Start, DateTimeOffset End);
}
