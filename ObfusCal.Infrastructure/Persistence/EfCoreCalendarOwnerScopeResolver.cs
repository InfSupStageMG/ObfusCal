using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class EfCoreCalendarOwnerScopeResolver(AppDbContext dbContext) : ICalendarOwnerScopeResolver
{
    public async Task<CalendarOwnerScope?> FindByEntraObjectIdAsync(string entraObjectId, CancellationToken ct = default)
    {
        return await dbContext.CalendarOwners
            .AsNoTracking()
            .Where(owner => owner.EntraObjectId == entraObjectId)
            .Select(owner => new CalendarOwnerScope(owner.Id, owner.EntraObjectId!, owner.Name))
            .SingleOrDefaultAsync(ct);
    }
}


