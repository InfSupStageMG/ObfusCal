using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class CalendarOwnerService(
    AppDbContext dbContext,
    IOptions<CalendarSourceOptions> calendarSourceOptions) : ICalendarOwnerService
{
    public async Task<IReadOnlyList<CalendarOwnerSummary>> ListAsync(CancellationToken ct = default)
    {
        return await dbContext.CalendarOwners
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new CalendarOwnerSummary(
                o.Id,
                o.Name,
                o.GraphConsentGrantedAtUtc != null,
                o.ICalFeeds.Count,
                o.PeerMappings.Count))
            .ToListAsync(ct);
    }

    public async Task<CalendarOwnerInfo?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.CalendarOwners
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new CalendarOwnerInfo(
                o.Id,
                o.Name,
                o.GraphConsentGrantedAtUtc != null))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<CalendarOwnerSummary> CreateAsync(string name, string? entraObjectId = null, CancellationToken ct = default)
    {
        var owner = new CalendarOwner
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            EntraObjectId = string.IsNullOrWhiteSpace(entraObjectId) ? null : entraObjectId.Trim(),
            CalendarSourcePluginId = string.IsNullOrWhiteSpace(calendarSourceOptions.Value.Provider)
                ? null
                : calendarSourceOptions.Value.Provider.Trim().ToLowerInvariant()
        };

        dbContext.CalendarOwners.Add(owner);
        await dbContext.SaveChangesAsync(ct);

        return new CalendarOwnerSummary(owner.Id, owner.Name, false, 0, 0);
    }
}


