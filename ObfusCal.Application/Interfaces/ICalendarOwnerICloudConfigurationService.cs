namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerICloudConfigurationService
{
    Task<CalendarOwnerICloudConfiguration?> GetConfigurationAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<CalendarOwnerICloudConfiguration?> GetConfigurationAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        CancellationToken ct = default);

    Task<CalendarOwnerICloudConfiguration?> SetConfigurationAsync(
        Guid calendarOwnerId,
        CalendarOwnerICloudConfigurationInput input,
        CancellationToken ct = default);

    Task<CalendarOwnerICloudConfiguration?> SetConfigurationAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        CalendarOwnerICloudConfigurationInput input,
        CancellationToken ct = default);

    Task<bool> ClearConfigurationAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<bool> ClearConfigurationAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default);
}

public sealed record CalendarOwnerICloudConfiguration(
    Guid CalendarOwnerId,
    bool IsConfigured,
    string? CalendarUrl,
    string? AppleIdHint);

public sealed record CalendarOwnerICloudConfigurationInput(
    string CalendarUrl,
    string AppleId,
    string AppSpecificPassword);

