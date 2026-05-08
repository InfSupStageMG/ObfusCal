using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Controllers;

/// <summary>
/// Composite that groups both consent service dependencies of <see cref="CalendarOwnersController"/>
/// to keep the constructor parameter count within StyleCop limits.
/// </summary>
public sealed record CalendarConsentServices(
    ICalendarOwnerGraphConsentService Graph,
    ICalendarOwnerGoogleConsentService Google);
