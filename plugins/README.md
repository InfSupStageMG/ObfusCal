# ObfusCal Plugin Directory

Place your plugin DLLs (and their immediate dependencies) in this directory. ObfusCal loads them at startup
using `AssemblyLoadContext` and registers discovered types into the DI container automatically.

A failed or incompatible DLL is logged and skipped; all other plugins continue to load normally.

---

## Authoring a Calendar Source Plugin

### 1 — Create a project

Name your project `ObfusCal.Plugins.<YourName>` and target `net10.0`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <!-- reference only Application and Domain — never Infrastructure -->
    <PackageReference Include="ObfusCal.Application" Version="*" />
  </ItemGroup>
</Project>
```

> **Naming convention matters.** Any project matching `ObfusCal.Plugins.*` is automatically built and
> copied to the `plugins/` output folder by the wildcard MSBuild targets in `ObfusCal.Api` and
> `ObfusCal.Tests`. You do **not** need to edit those `.csproj` files.

---

### 2 — Implement the calendar source contract

Implement `ICalendarSource` from `ObfusCal.Application.Interfaces` and annotate your class with
`[CalendarSourcePlugin]`:

```csharp
using ObfusCal.Application.Interfaces;
using ObfusCal.Domain.Models;

[CalendarSourcePlugin("acme", "Acme Calendar")]
public sealed class AcmeCalendarSource : ICalendarSource
{
    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        DateTimeOffset from, DateTimeOffset to,
        Guid? calendarOwnerId = null, CancellationToken ct = default)
    {
        // fetch events from the Acme Calendar API
        throw new NotImplementedException();
    }
}
```

The first argument to `[CalendarSourcePlugin]` is the stable **plugin ID** — lowercase, no spaces.
It is stored in database records, config files, and API responses. **Changing it after deployment
requires a data migration.**

---

### 3 — Provide setup UI metadata (optional but recommended)

Add `[CalendarSourcePluginUi]` to describe how the generic owner-detail UI should render your plugin's
setup form:

```csharp
[CalendarSourcePlugin("acme", "Acme Calendar")]
[CalendarSourcePluginUi(
    supportsMultipleInstances: true,
    configurationJsonTemplate: """{"calendarId":"primary","region":"eu"}""",
    secretDataJsonTemplate:    """{"apiKey":"<your-api-key>"}""",
    setupHint: "Create an API key at https://acme.example.com/api-keys and paste it in the Secret JSON field.")]
public sealed class AcmeCalendarSource : ICalendarSource { ... }
```

| Property                    | Purpose                                                         |
|-----------------------------|-----------------------------------------------------------------|
| `supportsMultipleInstances` | Whether an owner may have more than one instance of this plugin |
| `configurationJsonTemplate` | Pre-filled content for the **Configuration JSON** textarea      |
| `secretDataJsonTemplate`    | Pre-filled content for the **Secret JSON** textarea             |
| `setupHint`                 | Informational message shown above the Add button                |

---

### 4 — Declare plugin action buttons (optional)

If your plugin has a setup step that requires navigation (e.g. an OAuth consent flow), declare it with
`[CalendarSourcePluginAction]`. The attribute may be applied multiple times.

```csharp
[CalendarSourcePlugin("acme", "Acme Calendar")]
[CalendarSourcePluginUi(...)]
[CalendarSourcePluginAction(
    "acme-oauth",                                    // action ID (stable, lowercase)
    "Authorize Acme",                                // button label
    hint: "Opens the Acme OAuth flow for this source instance.")]
public sealed class AcmeCalendarSource : ICalendarSource { ... }
```

#### Built-in action IDs handled by the UI

| Action ID                 | Behaviour                                                                                                                |
|---------------------------|--------------------------------------------------------------------------------------------------------------------------|
| `google-instance-consent` | Calls `ICalendarOwnerGoogleConsentService.BuildAuthorizationUrlAsync` for the instance and navigates to the returned URL |
| `graph-instance-consent`  | Calls `ICalendarOwnerGraphConsentService.BuildAuthorizationUrlAsync` for the instance and navigates to the returned URL  |

Actions with an **unrecognised ID** show a warning message. To add support for a new action ID, open
`CalendarOwnerDetail.razor` and add a `case` to `InvokePluginActionAsync`.

The consent callback page (`/consent-callback`) handles the OAuth redirect for both built-in consent
actions. It reads `ownerId`, `instanceId`, and `plugin` from the query string and calls the appropriate
completion service.

---

### 5 — Readiness evaluation (optional)

If a source instance requires configuration before it can be used, also implement
`ICalendarSourceInstanceReadinessEvaluator`:

```csharp
[CalendarSourcePlugin("acme", "Acme Calendar")]
public sealed class AcmeCalendarSource : ICalendarSource, ICalendarSourceInstanceReadinessEvaluator
{
    public async Task<CalendarSourceReadiness> GetReadinessAsync(
        CalendarSourceInstanceContext instance,
        CancellationToken ct = default)
    {
        var hasKey = !string.IsNullOrWhiteSpace(instance.SecretDataJson);
        return hasKey
            ? CalendarSourceReadiness.Ready()
            : CalendarSourceReadiness.NotReady(
                "API key required.",
                "Paste your Acme API key in the Secret JSON field.");
    }

    // ... ICalendarSource implementation
}
```

Legacy owner-level readiness uses `ICalendarSourceReadinessEvaluator` (no `instance` parameter).

---

### 6 — Build and deploy

**During development** (if your project follows the naming convention `ObfusCal.Plugins.*`):

```
dotnet build ObfusCal.slnx
# The plugin DLL is automatically copied to ObfusCal.Api/bin/.../plugins/
```

**For production** — copy the plugin DLL and its dependencies into the `plugins/` sub-folder alongside
`ObfusCal.Api.dll`, then restart the API process.

---

## Obfuscation transformer plugins

Implement `IObfuscationTransformerPlugin` (event-level) or `IBusySlotTransformerPlugin` (slot-level)
from `ObfusCal.Domain.Obfuscation`:

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

Plugins are loaded as **trusted code** in the same process. There is no sandbox boundary. Do not load
plugins from untrusted sources.

---

## Selecting a calendar source per owner

Once registered, a plugin becomes selectable via the API:

```
PUT /api/calendar-owners/{id}/calendar/provider
{ "providerId": "acme" }
```

Or configure the application-wide default in `appsettings.json`:

```json
{
    "CalendarSource": {
        "Provider": "acme"
    }
}
```
