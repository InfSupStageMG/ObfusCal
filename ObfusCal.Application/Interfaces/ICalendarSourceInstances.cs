using ObfusCal.Domain.Models;

namespace ObfusCal.Application.Interfaces;

public interface ICalendarSourceInstanceService
{
    Task<IReadOnlyList<CalendarSourceInstanceSummary>> ListAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<CalendarSourceInstanceSummary?> CreateAsync(
        Guid calendarOwnerId,
        CreateCalendarSourceInstanceInput input,
        CancellationToken ct = default);

    Task<CalendarSourceInstanceSummary?> UpdateAsync(
        Guid calendarOwnerId,
        Guid instanceId,
        UpdateCalendarSourceInstanceInput input,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid calendarOwnerId, Guid instanceId, CancellationToken ct = default);
}

public interface ICalendarSourceInstanceStore
{
    Task<IReadOnlyList<CalendarSourceInstanceContext>> ListAsync(Guid calendarOwnerId, CancellationToken ct = default);
    Task<CalendarSourceInstanceContext?> GetAsync(Guid calendarOwnerId, Guid instanceId, CancellationToken ct = default);
    Task<CalendarSourceInstanceContext?> GetFirstAsync(Guid calendarOwnerId, string pluginId, CancellationToken ct = default);
    Task<bool> UpdateSecretDataAsync(Guid calendarOwnerId, Guid instanceId, string? secretDataJson, CancellationToken ct = default);
}

public interface ICalendarSourceSecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}

public interface ICalendarSourceInstanceHandler
{
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarSourceInstanceContext instance,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}

public interface ICalendarSourceInstanceReadinessEvaluator
{
    Task<CalendarSourceReadiness> GetReadinessAsync(CalendarSourceInstanceContext instance, CancellationToken ct = default);
}

public sealed record CalendarSourceInstanceContext(
    Guid Id,
    Guid CalendarOwnerId,
    string PluginId,
    string DisplayName,
    bool IsEnabled,
    string? ConfigurationJson,
    string? SecretDataJson,
    bool IsExternalPlugin);

public sealed record CalendarSourceInstanceSummary(
    Guid Id,
    Guid CalendarOwnerId,
    string PluginId,
    string DisplayName,
    string? ConfigurationJson,
    bool IsEnabled,
    bool IsReady,
    string Title,
    string? Detail,
    bool IsExternalPlugin);

public sealed record CreateCalendarSourceInstanceInput(
    string PluginId,
    string DisplayName,
    string? ConfigurationJson = null,
    string? SecretDataJson = null,
    bool IsEnabled = true);

public sealed record UpdateCalendarSourceInstanceInput(
    string? DisplayName = null,
    string? ConfigurationJson = null,
    string? SecretDataJson = null,
    bool? IsEnabled = null);

