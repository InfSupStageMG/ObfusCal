using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Sync;

public sealed class OutboundPeerSyncService(
    AppDbContext dbContext,
    ICalendarSource calendarSource,
    ObfuscationPipeline obfuscationPipeline,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService,
    IHttpClientFactory httpClientFactory,
    IOptions<SyncOptions> syncOptions,
    ILogger<OutboundPeerSyncService> logger)
    : IOutboundPeerSyncService
{
    private const string PeerIdHeaderName = "X-Peer-Id";
    private const string PeerApiKeyScheme = "ApiKey";
    private const string ShadowSlotsRelativePath = "api/shadow-slots";

    public async Task RunSyncCycleAsync(CancellationToken ct = default)
    {
        var options = syncOptions.Value;
        if (string.IsNullOrWhiteSpace(options.InstanceId) || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            logger.LogDebug(
                "Skipping outbound peer sync because Sync.InstanceId or Sync.ApiKey is not configured.");
            return;
        }

        var syncWindowStart = DateTimeOffset.UtcNow;
        var syncWindowEnd = syncWindowStart.AddDays(Math.Max(1, options.LookAheadDays));

        var ownerIds = await dbContext.CalendarOwners
            .AsNoTracking()
            .Select(owner => owner.Id)
            .ToListAsync(ct);

        foreach (var calendarOwnerId in ownerIds)
        {
            try
            {
                await SyncCalendarOwnerAsync(calendarOwnerId, syncWindowStart, syncWindowEnd, options, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Outbound peer sync failed for calendar owner {CalendarOwnerId}; continuing with next owner.",
                    calendarOwnerId);
            }
        }
    }

    private async Task SyncCalendarOwnerAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        SyncOptions options,
        CancellationToken ct)
    {
        var mappings = await dbContext.CalendarOwnerPeerMappings
            .AsNoTracking()
            .Where(mapping => mapping.CalendarOwnerId == calendarOwnerId)
            .Select(mapping => new PeerMappingTarget(
                mapping.CalendarOwnerRef,
                mapping.PeerConnection.InstanceId,
                mapping.PeerConnection.BaseAddress))
            .ToListAsync(ct);

        if (mappings.Count == 0)
            return;

        var events = await calendarSource.GetEventsAsync(from, to, calendarOwnerId, ct);
        var profile = await obfuscationProfileService.GetProfileAsync(
            calendarOwnerId,
            ObfuscationAuditContext.Client,
            ct);
        var busySlots = obfuscationPipeline.Process(
            events,
            calendarOwnerId.ToString(),
            ObfuscationAuditContext.Client,
            profile);

        foreach (var mapping in mappings)
            await PushToPeerAsync(mapping, busySlots, options, ct);

        logger.LogInformation(
            "Completed outbound peer sync for calendar owner {CalendarOwnerId} to {PeerCount} peer(s).",
            calendarOwnerId,
            mappings.Count);
    }

    private async Task PushToPeerAsync(
        PeerMappingTarget mapping,
        IReadOnlyList<ObfusCal.Domain.Models.BusySlot> busySlots,
        SyncOptions options,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(mapping.BaseAddress, UriKind.Absolute, out var baseUri))
        {
            logger.LogWarning(
                "Skipping outbound peer sync for peer {PeerId} because BaseAddress is invalid.",
                mapping.PeerInstanceId);
            return;
        }

        var endpoint = new Uri(baseUri, ShadowSlotsRelativePath);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = JsonContent.Create(new PeerShadowSlotsPushRequest(
            mapping.CalendarOwnerRef,
            busySlots.Select(slot => new PeerShadowSlot(
                slot.Start,
                slot.End,
                slot.Title,
                slot.Description,
                slot.AttendeeEmails,
                slot.Location)).ToArray()));

        request.Headers.Authorization = new AuthenticationHeaderValue(PeerApiKeyScheme, options.ApiKey);
        request.Headers.Add(PeerIdHeaderName, options.InstanceId);

        var client = httpClientFactory.CreateClient(nameof(OutboundPeerSyncService));

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Successfully pushed {BusySlotCount} obfuscated slot(s) for peer {PeerId} and calendar owner ref {CalendarOwnerRef}.",
                    busySlots.Count,
                    mapping.PeerInstanceId,
                    mapping.CalendarOwnerRef);
                return;
            }

            logger.LogWarning(
                "Peer push failed for peer {PeerId} and calendar owner ref {CalendarOwnerRef} with HTTP {StatusCode}; continuing sync cycle.",
                mapping.PeerInstanceId,
                mapping.CalendarOwnerRef,
                (int)response.StatusCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Peer push failed for peer {PeerId} and calendar owner ref {CalendarOwnerRef}; continuing sync cycle.",
                mapping.PeerInstanceId,
                mapping.CalendarOwnerRef);
        }
    }

    private sealed record PeerMappingTarget(Guid CalendarOwnerRef, string PeerInstanceId, string BaseAddress);

    private sealed record PeerShadowSlotsPushRequest(Guid CalendarOwnerRef, IReadOnlyList<PeerShadowSlot> Slots);

    private sealed record PeerShadowSlot(
        DateTimeOffset Start,
        DateTimeOffset End,
        string? Title,
        string? Description,
        IReadOnlyList<string>? AttendeeEmails,
        string? Location);
}
