using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Application.Interfaces;

public interface ICalendarOwnerObfuscationProfileService
{
    Task<IReadOnlyList<ObfuscationProfileSettings>> GetProfilesAsync(Guid calendarOwnerId, CancellationToken ct = default);

    Task<ObfuscationProfileSettings> GetProfileAsync(
        Guid calendarOwnerId,
        ObfuscationAuditContext context,
        CancellationToken ct = default);

    Task<ObfuscationProfileSettings> SetProfileAsync(
        Guid calendarOwnerId,
        ObfuscationProfileSettings profile,
        CancellationToken ct = default);
}

