using System.Text.Json;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Calendars;

namespace ObfusCal.Infrastructure.Persistence;

internal sealed class CalendarOwnerICloudConfigurationService(
    ICalendarSourceInstanceService calendarSourceInstanceService,
    ICalendarSourceInstanceStore calendarSourceInstanceStore,
    ICalendarSourceSecretProtector secretProtector)
    : ICalendarOwnerICloudConfigurationService
{
    private const string PluginId = "icloud";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<CalendarOwnerICloudConfiguration?> GetConfigurationAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId, PluginId, ct);
        if (instance is null)
            return null;

        return await GetConfigurationAsync(calendarOwnerId, instance.Id, ct);
    }

    public async Task<CalendarOwnerICloudConfiguration?> GetConfigurationAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct);
        if (instance is null || !string.Equals(instance.PluginId, PluginId, StringComparison.OrdinalIgnoreCase))
            return null;

        var configuration = ParseConfiguration(instance.ConfigurationJson);
        var secrets = ParseSecretData(instance.SecretDataJson);
        var unprotectedAppleId = TryUnprotect(secrets?.AppleId);
        var unprotectedAppPassword = TryUnprotect(secrets?.AppSpecificPassword);
        var isConfigured = !string.IsNullOrWhiteSpace(configuration?.CalendarUrl)
            && !string.IsNullOrWhiteSpace(unprotectedAppleId)
            && !string.IsNullOrWhiteSpace(unprotectedAppPassword);

        return new CalendarOwnerICloudConfiguration(
            calendarOwnerId,
            isConfigured,
            configuration?.CalendarUrl,
            MaskAppleId(unprotectedAppleId));
    }

    public async Task<CalendarOwnerICloudConfiguration?> SetConfigurationAsync(
        Guid calendarOwnerId,
        CalendarOwnerICloudConfigurationInput input,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId, PluginId, ct);
        if (instance is null)
        {
            var created = await calendarSourceInstanceService.CreateAsync(
                calendarOwnerId,
                new CreateCalendarSourceInstanceInput(PluginId, "iCloud Calendar"),
                ct);

            if (created is null)
                return null;

            instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, created.Id, ct);
        }

        if (instance is null)
            return null;

        return await SetConfigurationAsync(calendarOwnerId, instance.Id, input, ct);
    }

    public async Task<CalendarOwnerICloudConfiguration?> SetConfigurationAsync(
        Guid calendarOwnerId,
        Guid calendarSourceInstanceId,
        CalendarOwnerICloudConfigurationInput input,
        CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetAsync(calendarOwnerId, calendarSourceInstanceId, ct);
        if (instance is null || !string.Equals(instance.PluginId, PluginId, StringComparison.OrdinalIgnoreCase))
            return null;

        var updated = await calendarSourceInstanceService.UpdateAsync(
            calendarOwnerId,
            calendarSourceInstanceId,
            new UpdateCalendarSourceInstanceInput(
                ConfigurationJson: JsonSerializer.Serialize(
                    new ICloudCalendarSourceCore.ICloudCalendarInstanceConfiguration(input.CalendarUrl.Trim()),
                    JsonOptions),
                SecretDataJson: JsonSerializer.Serialize(
                    new ICloudCalendarSourceCore.ICloudCalendarInstanceSecretData(
                        secretProtector.Protect(input.AppleId.Trim()),
                        secretProtector.Protect(input.AppSpecificPassword.Trim())),
                    JsonOptions),
                IsEnabled: true),
            ct);

        if (updated is null)
            return null;

        return new CalendarOwnerICloudConfiguration(
            calendarOwnerId,
            true,
            input.CalendarUrl.Trim(),
            MaskAppleId(input.AppleId));
    }

    public async Task<bool> ClearConfigurationAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var instance = await calendarSourceInstanceStore.GetFirstAsync(calendarOwnerId, PluginId, ct);
        if (instance is null)
            return false;

        return await ClearConfigurationAsync(calendarOwnerId, instance.Id, ct);
    }

    public async Task<bool> ClearConfigurationAsync(Guid calendarOwnerId, Guid calendarSourceInstanceId, CancellationToken ct = default)
    {
        var updated = await calendarSourceInstanceService.UpdateAsync(
            calendarOwnerId,
            calendarSourceInstanceId,
            new UpdateCalendarSourceInstanceInput(
                ConfigurationJson: string.Empty,
                SecretDataJson: string.Empty,
                IsEnabled: false),
            ct);

        return updated is not null;
    }

    private string? TryUnprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return secretProtector.Unprotect(value);
        }
        catch
        {
            return null;
        }
    }


    private static string? MaskAppleId(string? appleId)
    {
        if (string.IsNullOrWhiteSpace(appleId))
            return null;

        var trimmed = appleId.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0)
            return trimmed.Length <= 2 ? "***" : $"{trimmed[0]}***{trimmed[^1]}";

        var localPart = trimmed[..atIndex];
        var domain = trimmed[atIndex..];
        return localPart.Length <= 2 ? $"{localPart[0]}***{domain}" : $"{localPart[0]}***{localPart[^1]}{domain}";
    }

    private static ICloudCalendarSourceCore.ICloudCalendarInstanceConfiguration? ParseConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ICloudCalendarSourceCore.ICloudCalendarInstanceConfiguration>(configurationJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static ICloudCalendarSourceCore.ICloudCalendarInstanceSecretData? ParseSecretData(string? secretDataJson)
    {
        if (string.IsNullOrWhiteSpace(secretDataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ICloudCalendarSourceCore.ICloudCalendarInstanceSecretData>(secretDataJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
