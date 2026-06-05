using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Infrastructure.Persistence;
using BusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Infrastructure.Sync;

/// <summary>
/// Background job component that processes raw events through the obfuscation pipeline,
/// stores the resulting snapshot locally, and triggers outbound calendar write-back if enabled.
/// </summary>
public sealed class CalendarOwnerAvailabilitySyncService(
    AppDbContext dbContext,
    ICalendarSourceResolver calendarSourceResolver,
    ObfuscationPipeline obfuscationPipeline,
    ICalendarOwnerObfuscationProfileService obfuscationProfileService,
    IShadowSlotStore shadowSlotStore,
    IOptions<SyncOptions> syncOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<CalendarOwnerAvailabilitySyncService> logger)
    : ICalendarOwnerAvailabilitySyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task RunSyncCycleAsync(CancellationToken ct = default, IProgress<SyncProgressUpdate>? progress = null)
    {
        var options = syncOptions.Value;
        var syncWindowStart = DateTimeOffset.UtcNow;
        var syncWindowEnd = syncWindowStart.AddDays(Math.Max(1, options.LookAheadDays));

        var ownerIds = await dbContext.CalendarOwners
            .AsNoTracking()
            .Select(owner => owner.Id)
            .ToListAsync(ct);

        var total = ownerIds.Count;
        for (var i = 0; i < total; i++)
        {
            var calendarOwnerId = ownerIds[i];
            progress?.Report(new SyncProgressUpdate($"Syncing owner {i + 1} of {total}…", i, total));
            try
            {
                var busySlots = await SyncCalendarOwnerAsync(calendarOwnerId, syncWindowStart, syncWindowEnd, ct);
                logger.LogInformation(
                    "Availability sync succeeded for calendar owner {CalendarOwnerId} with {BusySlotCount} busy slot(s).",
                    calendarOwnerId,
                    busySlots.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Availability sync failed for calendar owner {CalendarOwnerId}; continuing with next owner.",
                    calendarOwnerId);
                await RecordSyncResultAsync(calendarOwnerId, succeeded: false);
            }
        }

        progress?.Report(new SyncProgressUpdate("Availability sync complete.", total, total));
    }

    public async Task RunSyncForOwnerAsync(Guid calendarOwnerId, CancellationToken ct = default, IProgress<SyncProgressUpdate>? progress = null)
    {
        var options = syncOptions.Value;
        var syncWindowStart = DateTimeOffset.UtcNow;
        var syncWindowEnd = syncWindowStart.AddDays(Math.Max(1, options.LookAheadDays));

        try
        {
            progress?.Report(new SyncProgressUpdate("Fetching calendar events…", 0, 0));
            var busySlots = await SyncCalendarOwnerAsync(calendarOwnerId, syncWindowStart, syncWindowEnd, ct, progress);
            logger.LogInformation(
                "Availability sync succeeded for calendar owner {CalendarOwnerId} with {BusySlotCount} busy slot(s).",
                calendarOwnerId,
                busySlots.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Availability sync failed for calendar owner {CalendarOwnerId}.",
                calendarOwnerId);
            await RecordSyncResultAsync(calendarOwnerId, succeeded: false);
        }
    }

    private async Task<IReadOnlyList<BusySlot>> SyncCalendarOwnerAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct,
        IProgress<SyncProgressUpdate>? progress = null)
    {
        var calendarSource = await calendarSourceResolver.ResolveAsync(calendarOwnerId, ct);
        var events = await calendarSource.GetEventsAsync(from, to, calendarOwnerId, ct);
        progress?.Report(new SyncProgressUpdate("Applying obfuscation profile…", 0, 0));
        var profile = await obfuscationProfileService.GetProfileAsync(
            calendarOwnerId,
            ObfuscationAuditContext.Internal,
            ct);
        var busySlots = obfuscationPipeline.Process(
            events,
            calendarOwnerId.ToString(),
            ObfuscationAuditContext.Internal,
            profile);

        progress?.Report(new SyncProgressUpdate("Saving availability snapshot…", 0, 0));
        await ReplaceAvailabilitySnapshotAsync(calendarOwnerId, busySlots, ct);

        if (calendarSource is not ICalendarWriteBack writeBack) return busySlots;
        try
        {
            var owner = await dbContext.CalendarOwners
                .AsNoTracking()
                .SingleOrDefaultAsync(o => o.Id == calendarOwnerId, ct);

            if (owner?.WriteBackEnabled == true)
            {
                progress?.Report(new SyncProgressUpdate("Writing back to calendar…", 0, 0));
                var writeBackEnd = DateTimeOffset.UtcNow.AddDays(Math.Max(1, syncOptions.Value.WriteBackLookAheadDays));
                var shadowSlots = await shadowSlotStore.GetAllSlotsAsync(calendarOwnerId, from, writeBackEnd, ct);

                if (shadowSlots.Count == 0)
                {
                    await writeBack.WriteBackSlotsAsync(calendarOwnerId, [], owner.WriteBackPlaceholderTitle ?? syncOptions.Value.WriteBackPlaceholderTitle, from, writeBackEnd, ct);
                    return busySlots;
                }

                // Apply client-level obfuscation to shadow slots before write-back
                var obfuscatedShadowSlots = await ApplyClientObfuscationAsync(shadowSlots, calendarOwnerId, ct);

                if (obfuscatedShadowSlots.Count > 0)
                {
                    logger.LogInformation(
                        "Triggering write-back for calendar owner {CalendarOwnerId}: {ObfuscatedShadowSlotCount} obfuscated shadow slot(s) in window [{WriteBackStart:O}, {WriteBackEnd:O}).",
                        calendarOwnerId, obfuscatedShadowSlots.Count, from, writeBackEnd);
                }

                await writeBack.WriteBackSlotsAsync(calendarOwnerId, obfuscatedShadowSlots, owner.WriteBackPlaceholderTitle ?? syncOptions.Value.WriteBackPlaceholderTitle, from, writeBackEnd, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Write-back failed for calendar owner {CalendarOwnerId}; availability sync result is still recorded.",
                calendarOwnerId);
        }

        return busySlots;
    }

    private async Task ReplaceAvailabilitySnapshotAsync(
        Guid calendarOwnerId,
        IReadOnlyList<BusySlot> busySlots,
        CancellationToken ct)
    {
        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await ReplaceAvailabilitySnapshotCoreAsync(calendarOwnerId, busySlots, ct);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                logger.LogWarning("Concurrency conflict while persisting availability snapshot for calendar owner {CalendarOwnerId} (attempt {Attempt}/{MaxRetries}). Retrying.", calendarOwnerId, attempt, maxRetries);

                var conflictingEntries = dbContext.ChangeTracker.Entries<CalendarOwnerAvailabilitySlot>().ToList();
                foreach (var entry in conflictingEntries)
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }

    private async Task ReplaceAvailabilitySnapshotCoreAsync(
        Guid calendarOwnerId,
        IReadOnlyList<BusySlot> busySlots,
        CancellationToken ct)
    {
        var owner = await dbContext.CalendarOwners
            .SingleOrDefaultAsync(o => o.Id == calendarOwnerId, ct)
            ?? throw new InvalidOperationException($"Calendar owner {calendarOwnerId} was not found.");

        var existingSlots = await dbContext.CalendarOwnerAvailabilitySlots
            .Where(slot => slot.CalendarOwnerId == calendarOwnerId)
            .ToListAsync(ct);
        dbContext.CalendarOwnerAvailabilitySlots.RemoveRange(existingSlots);

        var entities = busySlots.Select(slot => new CalendarOwnerAvailabilitySlot
        {
            Id = Guid.NewGuid(),
            CalendarOwnerId = calendarOwnerId,
            SourceEventId = slot.SourceEventId,
            Start = slot.Start,
            End = slot.End,
            Title = slot.Title,
            Description = slot.Description,
            AttendeeEmails = slot.AttendeeEmails?.ToArray(),
            Location = slot.Location,
            SourceLabel = slot.SourceLabel,
            SourceName = slot.SourceName,
            ColorHex = slot.ColorHex,
            IsAllDay = slot.IsAllDay,
            SourceSlotsJson = SerializeSourceSlots(slot.SourceSlots)
        }).ToList();

        await dbContext.CalendarOwnerAvailabilitySlots.AddRangeAsync(entities, ct);

        owner.LastSyncedAt = DateTimeOffset.UtcNow;
        owner.LastSyncSucceeded = true;

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task RecordSyncResultAsync(Guid calendarOwnerId, bool succeeded)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var scopedDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await scopedDbContext.CalendarOwners.SingleOrDefaultAsync(o => o.Id == calendarOwnerId, CancellationToken.None);
            if (owner is null)
                return;

            owner.LastSyncedAt = DateTimeOffset.UtcNow;
            owner.LastSyncSucceeded = succeeded;
            await scopedDbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to persist availability sync metadata for calendar owner {CalendarOwnerId}.",
                calendarOwnerId);
        }
    }

    private static string? SerializeSourceSlots(IReadOnlyList<BusySlot>? sourceSlots)
    {
        if (sourceSlots is null || sourceSlots.Count == 0)
            return null;

        try
        {
            var dtos = sourceSlots.Select(s => new
            {
                s.SourceEventId,
                s.Start,
                s.End,
                s.Title,
                s.Description,
                s.AttendeeEmails,
                s.Location,
                s.SourceLabel,
                s.IsAllDay,
                s.ColorHex,
                s.SourceName
            }).ToArray();

            return JsonSerializer.Serialize(dtos, JsonOptions);
        }
        catch (NotSupportedException)
        {
            // If serialization fails, don't crash - data persistence should continue
            return null;
        }
        catch (JsonException)
        {
            // If serialization fails, don't crash - data persistence should continue
            return null;
        }
    }

    private async Task<IReadOnlyList<BusySlot>> ApplyClientObfuscationAsync(
        IReadOnlyList<BusySlot> shadowSlots,
        Guid calendarOwnerId,
        CancellationToken ct)
    {
        // Convert BusySlots back to CalendarEvents so we can run them through the obfuscation pipeline
        var eventsFromSlots = shadowSlots.Select(slot => new Domain.Models.CalendarEvent(
            slot.SourceEventId,
            slot.Title ?? string.Empty,
            slot.Description,
            slot.Start,
            slot.End,
            slot.AttendeeEmails ?? [],
            slot.Location,
            slot.SourceLabel,
            IsAllDay: slot.IsAllDay,
            ColorHex: slot.ColorHex,
            SourceName: slot.SourceName
        )).ToList();

        // Apply client-level obfuscation using the user's configured profile
        var clientProfile = await obfuscationProfileService.GetProfileAsync(
            calendarOwnerId,
            ObfuscationAuditContext.Client,
            ct);

        var obfuscatedSlots = obfuscationPipeline.Process(
            eventsFromSlots,
            calendarOwnerId.ToString(),
            ObfuscationAuditContext.Client,
            clientProfile);

        return obfuscatedSlots;
    }
}

