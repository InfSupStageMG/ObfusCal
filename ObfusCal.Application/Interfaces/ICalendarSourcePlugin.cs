namespace ObfusCal.Application.Interfaces;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CalendarSourcePluginAttribute(string id, string displayName) : Attribute
{
    public string Id { get; } = id;
    public string DisplayName { get; } = displayName;
}

public interface ICalendarSourceReadinessEvaluator
{
    Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default);
}

public interface ICalendarSourceResolver
{
    Task<ICalendarSource> ResolveAsync(Guid? calendarOwnerId = null, CancellationToken ct = default);
}

public interface ICalendarSourceCatalog
{
    IReadOnlyList<CalendarSourcePluginDescriptor> GetPlugins();
    CalendarSourcePluginDescriptor? GetPlugin(string pluginId);
}

public interface ICalendarOwnerCalendarSourceService
{
    Task<IReadOnlyList<CalendarSourceProviderInfo>> ListProvidersAsync(Guid calendarOwnerId, CancellationToken ct = default);
    Task<CalendarSourceSelection?> GetSelectionAsync(Guid calendarOwnerId, CancellationToken ct = default);
    Task<CalendarSourceSelection?> SetSelectionAsync(Guid calendarOwnerId, string pluginId, CancellationToken ct = default);
}

public sealed record CalendarSourcePluginDescriptor(
    string Id,
    string DisplayName,
    Type ImplementationType,
    bool IsExternalPlugin);

public sealed record CalendarSourceReadiness(bool IsReady, string Title, string? Detail = null)
{
    public static CalendarSourceReadiness Ready(string title = "Ready.", string? detail = null) =>
        new(true, title, detail);

    public static CalendarSourceReadiness NotReady(string title, string? detail = null) =>
        new(false, title, detail);
}

public sealed record CalendarSourceProviderInfo(
    string Id,
    string DisplayName,
    bool IsSelected,
    bool IsReady,
    string Title,
    string? Detail,
    bool IsExternalPlugin);

public sealed record CalendarSourceSelection(
    Guid CalendarOwnerId,
    string Id,
    string DisplayName,
    bool IsReady,
    string Title,
    string? Detail,
    bool IsExternalPlugin);


