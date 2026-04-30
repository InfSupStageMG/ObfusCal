# ObfusCal Plugin Directory

Place your plugin DLLs (and their immediate dependencies) in this directory. ObfusCal loads them at startup
using `AssemblyLoadContext` and registers discovered types into the DI container automatically.

A failed or incompatible DLL is logged and skipped; all other plugins continue to load normally.

---

## Calendar source plugins

Implement `ICalendarSource` from `ObfusCal.Application` and annotate your class with
`[CalendarSourcePlugin]` from the same namespace:

```csharp
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;

[CalendarSourcePlugin("google", "Google Workspace")]
public sealed class GoogleCalendarSource : ICalendarSource
{
    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from, DateTimeOffset to,
        Guid? calendarOwnerId = null, CancellationToken ct = default)
    {
        // fetch events from Google Calendar API here
        throw new NotImplementedException();
    }
}
```

The first argument to `[CalendarSourcePlugin]` is the stable, lowercase **plugin ID** used in configuration,
database records, and API responses. Choose carefully — changing it after deployment requires a data migration.

### Readiness evaluation (optional)

If your adapter requires setup before it can be used (e.g. OAuth consent, a configured feed URL), also implement
`ICalendarSourceReadinessEvaluator`:

```csharp
[CalendarSourcePlugin("google", "Google Workspace")]
public sealed class GoogleCalendarSource : ICalendarSource, ICalendarSourceReadinessEvaluator
{
    public async Task<CalendarSourceReadiness> GetReadinessAsync(Guid calendarOwnerId, CancellationToken ct = default)
    {
        var hasConsent = /* check your consent store */ false;
        return hasConsent
            ? CalendarSourceReadiness.Ready()
            : CalendarSourceReadiness.NotReady("Google consent required.", "Visit /calendar/consent to grant access.");
    }

    // ... ICalendarSource implementation
}
```

---

## Obfuscation transformer plugins

Implement `IObfuscationTransformerPlugin` (event-level) or `IBusySlotTransformerPlugin` (slot-level) from
`ObfusCal.Domain.Obfuscation`:

```csharp
using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation;

public sealed class RedactSubjectTransformer : IObfuscationTransformerPlugin
{
    public string Id => "redact-subject";
    public int Order => 50;          // lower = runs earlier in the pipeline

    public CalendarEvent Transform(CalendarEvent calendarEvent, ObfuscationProfileSettings profile)
        => calendarEvent with { Title = "[REDACTED]" };
}
```

No attribute is needed — the plugin system discovers any concrete class that implements the interface.

---

## Version compatibility

| Contract assembly      | Compatible if …                        |
|------------------------|----------------------------------------|
| `ObfusCal.Application` | Same major version as the running host |
| `ObfusCal.Domain`      | Same major version as the running host |

Plugins are loaded as **trusted code** in the same process. There is no sandbox boundary. Do not load plugins
from untrusted sources.

---

## Selecting a calendar source per owner

Once registered, a plugin becomes selectable via the API:

```
PUT /api/calendar-owners/{id}/calendar/provider
{ "providerId": "google" }
```

Or configure the application-wide default in `appsettings.json`:

```json
{
  "CalendarSource": {
    "Provider": "google"
  }
}
```

