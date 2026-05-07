using ObfusCal.Application.Interfaces;

namespace ObfusCal.Tests.Helpers;

internal sealed class FakeCalendarSourceInstanceService(Func<Guid, bool>? ownerExists = null)
    : ICalendarSourceInstanceService, ICalendarSourceInstanceStore
{
    private readonly List<CalendarSourceInstanceContext> _instances = [];

    public Task<IReadOnlyList<CalendarSourceInstanceSummary>> ListAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var items = _instances
            .Where(instance => instance.CalendarOwnerId == calendarOwnerId)
            .OrderBy(instance => instance.PluginId)
            .ThenBy(instance => instance.DisplayName)
            .Select(instance => new CalendarSourceInstanceSummary(
                instance.Id,
                instance.CalendarOwnerId,
                instance.PluginId,
                instance.DisplayName,
                instance.ConfigurationJson,
                instance.IsEnabled,
                true,
                "Configured.",
                null,
                instance.IsExternalPlugin))
            .ToList();

        return Task.FromResult<IReadOnlyList<CalendarSourceInstanceSummary>>(items);
    }

    Task<IReadOnlyList<CalendarSourceInstanceContext>> ICalendarSourceInstanceStore.ListAsync(Guid calendarOwnerId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CalendarSourceInstanceContext>>(_instances
            .Where(instance => instance.CalendarOwnerId == calendarOwnerId)
            .OrderBy(instance => instance.PluginId)
            .ThenBy(instance => instance.DisplayName)
            .ToList());

    public Task<CalendarSourceInstanceContext?> GetAsync(Guid calendarOwnerId, Guid instanceId, CancellationToken ct = default)
        => Task.FromResult(_instances.SingleOrDefault(instance => instance.CalendarOwnerId == calendarOwnerId && instance.Id == instanceId));

    public Task<CalendarSourceInstanceContext?> GetFirstAsync(Guid calendarOwnerId, string pluginId, CancellationToken ct = default)
        => Task.FromResult(_instances
            .Where(instance => instance.CalendarOwnerId == calendarOwnerId && string.Equals(instance.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(instance => instance.DisplayName)
            .FirstOrDefault());

    public Task<CalendarSourceInstanceSummary?> CreateAsync(
        Guid calendarOwnerId,
        CreateCalendarSourceInstanceInput input,
        CancellationToken ct = default)
    {
        if (ownerExists is not null && !ownerExists(calendarOwnerId))
            return Task.FromResult<CalendarSourceInstanceSummary?>(null);

        var context = new CalendarSourceInstanceContext(
            Guid.NewGuid(),
            calendarOwnerId,
            input.PluginId.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(input.DisplayName) ? input.PluginId : input.DisplayName.Trim(),
            input.IsEnabled,
            input.ConfigurationJson,
            input.SecretDataJson,
            false);

        _instances.Add(context);
        return Task.FromResult<CalendarSourceInstanceSummary?>(new CalendarSourceInstanceSummary(
            context.Id,
            context.CalendarOwnerId,
            context.PluginId,
            context.DisplayName,
            context.ConfigurationJson,
            context.IsEnabled,
            true,
            "Configured.",
            null,
            context.IsExternalPlugin));
    }

    public Task<CalendarSourceInstanceSummary?> UpdateAsync(
        Guid calendarOwnerId,
        Guid instanceId,
        UpdateCalendarSourceInstanceInput input,
        CancellationToken ct = default)
    {
        var existing = _instances.SingleOrDefault(instance => instance.CalendarOwnerId == calendarOwnerId && instance.Id == instanceId);
        if (existing is null)
            return Task.FromResult<CalendarSourceInstanceSummary?>(null);

        var updated = existing with
        {
            DisplayName = input.DisplayName ?? existing.DisplayName,
            ConfigurationJson = input.ConfigurationJson ?? existing.ConfigurationJson,
            SecretDataJson = input.SecretDataJson ?? existing.SecretDataJson,
            IsEnabled = input.IsEnabled ?? existing.IsEnabled
        };

        _instances.Remove(existing);
        _instances.Add(updated);

        return Task.FromResult<CalendarSourceInstanceSummary?>(new CalendarSourceInstanceSummary(
            updated.Id,
            updated.CalendarOwnerId,
            updated.PluginId,
            updated.DisplayName,
            updated.ConfigurationJson,
            updated.IsEnabled,
            true,
            "Configured.",
            null,
            updated.IsExternalPlugin));
    }

    public Task<bool> DeleteAsync(Guid calendarOwnerId, Guid instanceId, CancellationToken ct = default)
    {
        var existing = _instances.SingleOrDefault(instance => instance.CalendarOwnerId == calendarOwnerId && instance.Id == instanceId);
        if (existing is null)
            return Task.FromResult(false);

        _instances.Remove(existing);
        return Task.FromResult(true);
    }

    public Task<bool> UpdateSecretDataAsync(Guid calendarOwnerId, Guid instanceId, string? secretDataJson, CancellationToken ct = default)
    {
        var existing = _instances.SingleOrDefault(instance => instance.CalendarOwnerId == calendarOwnerId && instance.Id == instanceId);
        if (existing is null)
            return Task.FromResult(false);

        _instances.Remove(existing);
        _instances.Add(existing with { SecretDataJson = secretDataJson });
        return Task.FromResult(true);
    }
}



