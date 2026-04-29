using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class CalendarOwnerObfuscationProfileService(AppDbContext dbContext)
    : ICalendarOwnerObfuscationProfileService
{
    public async Task<IReadOnlyList<ObfuscationProfileSettings>> GetProfilesAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        await EnsureDefaultProfilesAsync(calendarOwnerId, ct);

        return await dbContext.ObfuscationProfiles
            .AsNoTracking()
            .Where(profile => profile.CalendarOwnerId == calendarOwnerId)
            .OrderBy(profile => profile.Context)
            .Select(profile => ToSettings(profile))
            .ToListAsync(ct);
    }

    public async Task<ObfuscationProfileSettings> GetProfileAsync(
        Guid calendarOwnerId,
        ObfuscationAuditContext context,
        CancellationToken ct = default)
    {
        await EnsureDefaultProfilesAsync(calendarOwnerId, ct);

        var profile = await dbContext.ObfuscationProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.CalendarOwnerId == calendarOwnerId && p.Context == context,
                ct);

        return profile is null
            ? ObfuscationProfileSettings.CreateDefault(context)
            : ToSettings(profile);
    }

    public async Task<ObfuscationProfileSettings> SetProfileAsync(
        Guid calendarOwnerId,
        ObfuscationProfileSettings profile,
        CancellationToken ct = default)
    {
        if (profile.RoundingIntervalMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(profile.RoundingIntervalMinutes), "Rounding interval must be greater than zero.");

        await EnsureDefaultProfilesAsync(calendarOwnerId, ct);

        var existing = await dbContext.ObfuscationProfiles
            .SingleAsync(
                p => p.CalendarOwnerId == calendarOwnerId && p.Context == profile.Context,
                ct);

        existing.RemoveTitle = profile.RemoveTitle;
        existing.RemoveDescription = profile.RemoveDescription;
        existing.RemoveLocation = profile.RemoveLocation;
        existing.RemoveAttendees = profile.RemoveAttendees;
        existing.RoundTimes = profile.RoundTimes;
        existing.RoundingIntervalMinutes = profile.RoundingIntervalMinutes;
        existing.MergeBlocks = profile.MergeBlocks;

        await dbContext.SaveChangesAsync(ct);

        return ToSettings(existing);
    }

    private async Task EnsureDefaultProfilesAsync(Guid calendarOwnerId, CancellationToken ct)
    {
        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(owner => owner.Id == calendarOwnerId, ct);

        if (!ownerExists)
            return;

        var existingContexts = await dbContext.ObfuscationProfiles
            .Where(profile => profile.CalendarOwnerId == calendarOwnerId)
            .Select(profile => profile.Context)
            .ToListAsync(ct);

        var createdAny = false;

        foreach (var context in Enum.GetValues<ObfuscationAuditContext>())
        {
            if (existingContexts.Contains(context))
                continue;

            var defaults = ObfuscationProfileSettings.CreateDefault(context);
            dbContext.ObfuscationProfiles.Add(new ObfuscationProfile
            {
                Id = Guid.NewGuid(),
                CalendarOwnerId = calendarOwnerId,
                Context = defaults.Context,
                RemoveTitle = defaults.RemoveTitle,
                RemoveDescription = defaults.RemoveDescription,
                RemoveLocation = defaults.RemoveLocation,
                RemoveAttendees = defaults.RemoveAttendees,
                RoundTimes = defaults.RoundTimes,
                RoundingIntervalMinutes = defaults.RoundingIntervalMinutes,
                MergeBlocks = defaults.MergeBlocks
            });
            createdAny = true;
        }

        if (createdAny)
            await dbContext.SaveChangesAsync(ct);
    }

    private static ObfuscationProfileSettings ToSettings(ObfuscationProfile profile) =>
        new(
            profile.Context,
            profile.RemoveTitle,
            profile.RemoveDescription,
            profile.RemoveLocation,
            profile.RemoveAttendees,
            profile.RoundTimes,
            profile.RoundingIntervalMinutes,
            profile.MergeBlocks);
}

