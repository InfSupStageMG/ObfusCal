namespace ObfusCal.Application.Interfaces;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CalendarSourcePluginAttribute(string id, string displayName) : Attribute
{
    public string Id { get; } = id;
    public string DisplayName { get; } = displayName;
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CalendarSourcePluginUiAttribute(
    bool supportsMultipleInstances = true,
    string? configurationJsonTemplate = null,
    string? secretDataJsonTemplate = null,
    string? setupHint = null) : Attribute
{
    public bool SupportsMultipleInstances { get; } = supportsMultipleInstances;
    public string? ConfigurationJsonTemplate { get; } = configurationJsonTemplate;
    public string? SecretDataJsonTemplate { get; } = secretDataJsonTemplate;
    public string? SetupHint { get; } = setupHint;
}

/// <summary>
/// Declares a named action button that ObfusCal's owner-detail UI will render for every source instance
/// of this plugin.  Use well-known <see cref="ActionId"/> values so the built-in UI handlers can
/// recognise them; custom IDs are surfaced as plain buttons that link to the owner detail page.
/// <para>
/// Built-in action IDs:
/// <list type="bullet">
///   <item><c>google-instance-consent</c> - initiates Google OAuth for a source instance</item>
///   <item><c>graph-instance-consent-readonly</c> - initiates Microsoft Graph OAuth (read-only) for a source instance</item>
///   <item><c>graph-instance-consent</c>  - initiates Microsoft Graph OAuth for a source instance</item>
/// </list>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CalendarSourcePluginActionAttribute(
    string actionId,
    string label,
    string? hint = null) : Attribute
{
    public string ActionId { get; } = actionId;
    public string Label { get; } = label;
    public string? Hint { get; } = hint;
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
    bool IsExternalPlugin,
    CalendarSourcePluginUiDescriptor? Ui = null);

public sealed record CalendarSourcePluginUiDescriptor(
    bool SupportsMultipleInstances,
    string? ConfigurationJsonTemplate,
    string? SecretDataJsonTemplate,
    string? SetupHint,
    IReadOnlyList<CalendarSourcePluginActionDescriptor> Actions);

public sealed record CalendarSourcePluginActionDescriptor(
    string ActionId,
    string Label,
    string? Hint);

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


