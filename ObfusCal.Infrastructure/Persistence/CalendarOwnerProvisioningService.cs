using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class CalendarOwnerProvisioningService(
    AppDbContext dbContext,
    IOptions<CalendarSourceOptions> calendarSourceOptions) : ICalendarOwnerProvisioningService
{
    public async Task<CalendarOwnerScope> EnsureForEntraUserAsync(
        string entraObjectId,
        string? displayName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entraObjectId))
            throw new ArgumentException("An Entra object ID is required.", nameof(entraObjectId));

        var normalizedObjectId = entraObjectId.Trim();
        var existing = await FindScopeAsync(normalizedObjectId, ct);
        if (existing is not null)
            return existing;

        var owner = new CalendarOwner
        {
            Id = Guid.NewGuid(),
            Name = NormalizeDisplayName(displayName, normalizedObjectId),
            EntraObjectId = normalizedObjectId,
            CalendarSourcePluginId = NormalizeProvider(calendarSourceOptions.Value.Provider)
        };

        dbContext.CalendarOwners.Add(owner);

        try
        {
            await dbContext.SaveChangesAsync(ct);
            return new CalendarOwnerScope(owner.Id, normalizedObjectId, owner.Name);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(owner).State = EntityState.Detached;

            existing = await FindScopeAsync(normalizedObjectId, ct);
            if (existing is not null)
                return existing;

            throw;
        }
    }

    private async Task<CalendarOwnerScope?> FindScopeAsync(string entraObjectId, CancellationToken ct)
    {
        return await dbContext.CalendarOwners
            .AsNoTracking()
            .Where(owner => owner.EntraObjectId == entraObjectId)
            .Select(owner => new CalendarOwnerScope(owner.Id, owner.EntraObjectId!, owner.Name))
            .SingleOrDefaultAsync(ct);
    }

    private static string NormalizeDisplayName(string? displayName, string entraObjectId)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? $"User {entraObjectId}"
            : displayName.Trim();
    }

    private static string? NormalizeProvider(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? null
            : provider.Trim().ToLowerInvariant();
    }
}

