using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;

namespace ObfusCal.Infrastructure.Calendars;

internal sealed class CalendarSourceInstanceService(
    AppDbContext dbContext,
    ICalendarSourceCatalog catalog,
    ICalendarSourceSecretProtector secretProtector,
    IServiceProvider serviceProvider)
    : ICalendarSourceInstanceService, ICalendarSourceInstanceStore
{
    public async Task<IReadOnlyList<CalendarSourceInstanceSummary>> ListAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var instances = await dbContext.CalendarSourceInstances
            .AsNoTracking()
            .Where(instance => instance.CalendarOwnerId == calendarOwnerId)
            .OrderBy(instance => instance.PluginId)
            .ThenBy(instance => instance.DisplayName)
            .ToListAsync(ct);

        var summaries = new List<CalendarSourceInstanceSummary>(instances.Count);
        foreach (var context in instances.Select(ToContext))
        {
            var readiness = await GetReadinessAsync(context, ct);
            summaries.Add(new CalendarSourceInstanceSummary(
                context.Id,
                context.CalendarOwnerId,
                context.PluginId,
                context.DisplayName,
                context.ConfigurationJson,
                context.IsEnabled,
                readiness.IsReady,
                readiness.Title,
                readiness.Detail,
                context.IsExternalPlugin));
        }

        return summaries;
    }

    async Task<IReadOnlyList<CalendarSourceInstanceContext>> ICalendarSourceInstanceStore.ListAsync(Guid calendarOwnerId, CancellationToken ct)
    {
        var instances = await dbContext.CalendarSourceInstances
            .AsNoTracking()
            .Where(instance => instance.CalendarOwnerId == calendarOwnerId)
            .OrderBy(instance => instance.PluginId)
            .ThenBy(instance => instance.DisplayName)
            .ToListAsync(ct);

        return instances.Select(ToContext).ToList();
    }

    public async Task<CalendarSourceInstanceContext?> GetAsync(Guid calendarOwnerId, Guid instanceId, CancellationToken ct = default)
    {
        var instance = await dbContext.CalendarSourceInstances
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CalendarOwnerId == calendarOwnerId && x.Id == instanceId, ct);

        return instance is null ? null : ToContext(instance);
    }

    public async Task<CalendarSourceInstanceContext?> GetFirstAsync(Guid calendarOwnerId, string pluginId, CancellationToken ct = default)
    {
        var normalizedPluginId = NormalizePluginId(pluginId);
        var instance = await dbContext.CalendarSourceInstances
            .AsNoTracking()
            .Where(x => x.CalendarOwnerId == calendarOwnerId && x.PluginId == normalizedPluginId)
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return instance is null ? null : ToContext(instance);
    }

    public async Task<CalendarSourceInstanceSummary?> CreateAsync(
        Guid calendarOwnerId,
        CreateCalendarSourceInstanceInput input,
        CancellationToken ct = default)
    {
        var plugin = catalog.GetPlugin(input.PluginId)
            ?? throw new InvalidOperationException($"Calendar source plugin '{input.PluginId}' is not registered.");

        var ownerExists = await dbContext.CalendarOwners
            .AsNoTracking()
            .AnyAsync(owner => owner.Id == calendarOwnerId, ct);

        if (!ownerExists)
            return null;

        var displayName = string.IsNullOrWhiteSpace(input.DisplayName)
            ? plugin.DisplayName
            : input.DisplayName.Trim();

        var instance = new CalendarSourceInstance
        {
            Id = Guid.NewGuid(),
            CalendarOwnerId = calendarOwnerId,
            PluginId = plugin.Id,
            DisplayName = displayName,
            IsEnabled = input.IsEnabled,
            ConfigurationJson = input.ConfigurationJson,
            SecretDataJson = input.SecretDataJson is null ? null : secretProtector.Protect(input.SecretDataJson),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.CalendarSourceInstances.Add(instance);
        await dbContext.SaveChangesAsync(ct);

        var context = ToContext(instance);
        var readiness = await GetReadinessAsync(context, ct);
        return new CalendarSourceInstanceSummary(
            context.Id,
            context.CalendarOwnerId,
            context.PluginId,
            context.DisplayName,
            context.ConfigurationJson,
            context.IsEnabled,
            readiness.IsReady,
            readiness.Title,
            readiness.Detail,
            context.IsExternalPlugin);
    }

    public async Task<CalendarSourceInstanceSummary?> UpdateAsync(
        Guid calendarOwnerId,
        Guid instanceId,
        UpdateCalendarSourceInstanceInput input,
        CancellationToken ct = default)
    {
        var instance = await dbContext.CalendarSourceInstances
            .SingleOrDefaultAsync(x => x.CalendarOwnerId == calendarOwnerId && x.Id == instanceId, ct);

        if (instance is null)
            return null;

        if (input.DisplayName is not null)
            instance.DisplayName = string.IsNullOrWhiteSpace(input.DisplayName) ? instance.DisplayName : input.DisplayName.Trim();

        if (input.ConfigurationJson is not null)
            instance.ConfigurationJson = input.ConfigurationJson;

        if (input.SecretDataJson is not null)
            instance.SecretDataJson = secretProtector.Protect(input.SecretDataJson);

        if (input.IsEnabled is { } isEnabled)
            instance.IsEnabled = isEnabled;

        instance.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var context = ToContext(instance);
        var readiness = await GetReadinessAsync(context, ct);
        return new CalendarSourceInstanceSummary(
            context.Id,
            context.CalendarOwnerId,
            context.PluginId,
            context.DisplayName,
            context.ConfigurationJson,
            context.IsEnabled,
            readiness.IsReady,
            readiness.Title,
            readiness.Detail,
            context.IsExternalPlugin);
    }

    public async Task<bool> DeleteAsync(Guid calendarOwnerId, Guid instanceId, CancellationToken ct = default)
    {
        var instance = await dbContext.CalendarSourceInstances
            .SingleOrDefaultAsync(x => x.CalendarOwnerId == calendarOwnerId && x.Id == instanceId, ct);

        if (instance is null)
            return false;

        dbContext.CalendarSourceInstances.Remove(instance);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateSecretDataAsync(Guid calendarOwnerId, Guid instanceId, string? secretDataJson, CancellationToken ct = default)
    {
        var instance = await dbContext.CalendarSourceInstances
            .SingleOrDefaultAsync(x => x.CalendarOwnerId == calendarOwnerId && x.Id == instanceId, ct);

        if (instance is null)
            return false;

        instance.SecretDataJson = secretDataJson is null ? null : secretProtector.Protect(secretDataJson);
        instance.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private async Task<CalendarSourceReadiness> GetReadinessAsync(CalendarSourceInstanceContext instance, CancellationToken ct)
    {
        var plugin = catalog.GetPlugin(instance.PluginId);
        if (plugin is null)
            return CalendarSourceReadiness.NotReady("Calendar source plugin is no longer available.");

        var source = (ICalendarSource)serviceProvider.GetRequiredService(plugin.ImplementationType);
        if (source is ICalendarSourceInstanceReadinessEvaluator instanceEvaluator)
            return await instanceEvaluator.GetReadinessAsync(instance, ct);

        if (source is ICalendarSourceReadinessEvaluator legacyEvaluator)
            return await legacyEvaluator.GetReadinessAsync(instance.CalendarOwnerId, ct);

        return CalendarSourceReadiness.Ready();
    }

    private CalendarSourceInstanceContext ToContext(CalendarSourceInstance instance)
    {
        var plugin = catalog.GetPlugin(instance.PluginId);
        return new CalendarSourceInstanceContext(
            instance.Id,
            instance.CalendarOwnerId,
            instance.PluginId,
            instance.DisplayName,
            instance.IsEnabled,
            instance.ConfigurationJson,
            TryUnprotectSecret(instance.SecretDataJson),
            plugin?.IsExternalPlugin ?? false);
    }

    private string? TryUnprotectSecret(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
            return null;
        try
        {
            return secretProtector.Unprotect(protectedValue);
        }
        catch (Exception)
        {
            // Data was not encrypted (legacy plaintext data) or key material changed.
            // Return null so the plugin treats the instance as not configured.
            return null;
        }
    }

    private static string NormalizePluginId(string pluginId) => pluginId.Trim().ToLowerInvariant();
}

