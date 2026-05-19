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
    public async Task RunSyncCycleAsync(CancellationToken ct = default)
    {
        var options = syncOptions.Value;
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
    }

    public async Task RunSyncForOwnerAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var options = syncOptions.Value;
        var syncWindowStart = DateTimeOffset.UtcNow;
        var syncWindowEnd = syncWindowStart.AddDays(Math.Max(1, options.LookAheadDays));

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
                "Availability sync failed for calendar owner {CalendarOwnerId}.",
                calendarOwnerId);
            await RecordSyncResultAsync(calendarOwnerId, succeeded: false);
        }
    }

    private async Task<IReadOnlyList<BusySlot>> SyncCalendarOwnerAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var calendarSource = await calendarSourceResolver.ResolveAsync(calendarOwnerId, ct);
        var events = await calendarSource.GetEventsAsync(from, to, calendarOwnerId, ct);
        var profile = await obfuscationProfileService.GetProfileAsync(
            calendarOwnerId,
            ObfuscationAuditContext.Internal,
            ct);
        var busySlots = obfuscationPipeline.Process(
            events,
            calendarOwnerId.ToString(),
            ObfuscationAuditContext.Internal,
            profile);

        await ReplaceAvailabilitySnapshotAsync(calendarOwnerId, busySlots, ct);

        if (calendarSource is not ICalendarWriteBack writeBack) return busySlots;
        try
        {
            var owner = await dbContext.CalendarOwners
                .AsNoTracking()
                .SingleOrDefaultAsync(o => o.Id == calendarOwnerId, ct);

            if (owner?.WriteBackEnabled == true)
            {
                var writeBackEnd = DateTimeOffset.UtcNow.AddDays(Math.Max(1, syncOptions.Value.WriteBackLookAheadDays));
                var shadowSlots = await shadowSlotStore.GetAllSlotsAsync(calendarOwnerId, from, writeBackEnd, ct);

                logger.LogInformation(
                    "Write-back starting for calendar owner {CalendarOwnerId}: {ShadowSlotCount} shadow slot(s) found in window [{WriteBackStart:O}, {WriteBackEnd:O}).",
                    calendarOwnerId, shadowSlots.Count, from, writeBackEnd);

                var placeholderTitle = string.IsNullOrWhiteSpace(owner.WriteBackPlaceholderTitle)
                    ? syncOptions.Value.WriteBackPlaceholderTitle
                    : owner.WriteBackPlaceholderTitle;

                await writeBack.WriteBackSlotsAsync(calendarOwnerId, shadowSlots, placeholderTitle, from, writeBackEnd, ct);
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
                logger.LogWarning(
                    "Concurrency conflict while persisting availability snapshot for calendar owner {CalendarOwnerId} (attempt {Attempt}/{MaxRetries}). Retrying.",
                    calendarOwnerId,
                    attempt,
                    maxRetries);

                // Detach all tracked entities so the next attempt starts clean.
                foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
                    entry.State = EntityState.Detached;
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
                s.Location
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
}

