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
    IOptions<SyncOptions> syncOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<CalendarOwnerAvailabilitySyncService> logger)
    : ICalendarOwnerAvailabilitySyncService
{
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
            ObfuscationAuditContext.Client,
            ct);
        var busySlots = obfuscationPipeline.Process(
            events,
            calendarOwnerId.ToString(),
            ObfuscationAuditContext.Client,
            profile);

        await ReplaceAvailabilitySnapshotAsync(calendarOwnerId, busySlots, ct);
        return busySlots;
    }

    private async Task ReplaceAvailabilitySnapshotAsync(
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
            Location = slot.Location
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
}

