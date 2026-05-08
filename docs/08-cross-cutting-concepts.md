# 8. Cross-cutting Concepts

## Privacy & Data Minimisation

Raw calendar events are never written to the database. They exist only as in-memory objects during a single pipeline
execution and are discarded immediately after. The obfuscation pipeline is the gating condition for storage; only its
output (`BusySlot`) is persisted.

| Data                         | Persisted       | In Memory            | Sent to Peers |
|------------------------------|-----------------|----------------------|---------------|
| Raw meeting details          | Never           | During pipeline only | Never         |
| Obfuscated BusySlot          | Yes             | Yes                  | Yes           |
| User ID and sync state       | Yes             | Yes                  | Never         |
| Obfuscation profile settings | Yes             | Yes                  | Never         |
| OAuth refresh tokens         | Yes (encrypted) | Yes                  | Never         |

## Security

**Human authentication:** All user-facing access is secured via Single Sign-On through Info Support's Entra ID (Azure
AD) using OpenID Connect. This automatically inherits existing conditional access policies including MFA.

**Sysadmin authorization:** Administrative peer-management endpoints under `/api/admin/*` require the Entra ID app role
`Sysadmin`. Authenticated users without that role receive `403 Forbidden`.

**Machine-to-machine authentication:** Peer sync endpoints require `Authorization: ApiKey ...`. Incoming credentials
are verified against hashed `PeerConnection.ApiKeyHash` values, and outbound pushes include both `Authorization`
and `X-Peer-Id` headers so the receiving instance can authenticate and correlate the sender without trusting the
peer ID header by itself.

**Peer credential lifecycle:** Sysadmins approve pending peer requests by setting the peer base URL and generating a
cryptographically secure API key. Keys are stored as salted PBKDF2-SHA256 hashes (210000 iterations); plaintext is
returned once in the approval/rotation response and is never logged. Revoking a peer sets `PeerConnection.RevokedAt`
and blocks authentication immediately.

**Peer scopes and replay protection:** Each peer stores allowed scopes in `PeerConnection.Scopes` and the API enforces
scope checks per endpoint (`push_shadow_slots` for `POST /api/shadow-slots`, `pull_busy_slots` for
`GET /api/sync/busy-slots/{calendarOwnerRef}`). Peer sync requests include `X-Peer-Timestamp` and are accepted only
within `Sync:PeerRequestTimestampToleranceSeconds` (default 300 seconds) to limit naive replay attacks.


**Credential encryption and key persistence:** Microsoft Graph OAuth refresh tokens and iCloud credentials are encrypted
at rest using the .NET Data Protection API (DPAPI) before being written to the database. A database breach yields only
ciphertext.

Encryption keys are stored in the container's `/dataprotection/keys` directory, which must be mounted as a persistent
volume. Without key persistence:

- Encryption keys are regenerated on each container restart
- Previously encrypted credentials become unreadable and must be re-entered
- Users see errors like "iCloud credentials could not be read"

Each API instance must have its own isolated DataProtection key store; keys must never be shared between instances.

**Centralized secret access:** Runtime secret reads are routed through `ISecretProvider` instead of direct
`IConfiguration` access in business logic. The default `EnvironmentSecretProvider` supports standard .NET
environment-key mapping (for example `GraphConsent:ClientSecret` -> `GRAPHCONSENT__CLIENTSECRET`). An
`ExternalSecretProvider` stub exists behind the same abstraction for later integration with a managed secret store.

**Startup validation:** `SecretStartupValidator` checks required secrets during startup and fails fast with a clear
error when critical values are missing. This prevents deferred runtime failures in database and OAuth flows.

**OAuth redirect discipline:** Google Calendar OAuth uses an exact redirect URI match. ObfusCal can override the
runtime callback origin via `GoogleConsent:RedirectUri` / `GOOGLECONSENT__REDIRECTURI` so container, proxy, and local
debugging setups can use a single registered callback. `.local` redirect domains are rejected early because Google does
not accept them for this flow.

**Data scoping:** After authentication, the user's Entra ID Object ID is extracted from the JWT token and used as a
strict data boundary at the repository layer. A user can only access their own events, slots, and configuration.

**Inbound payload validation:** API request models use DataAnnotations and ASP.NET Core global model-state handling.
Validation failures return uniform `400` `ValidationProblemDetails` responses (without stack traces).

**SSRF resistance for outbound URL inputs:** User/admin-provided URLs (for iCal feeds and peer base URLs) are validated
through a shared URL safety service. Only `https` URLs are accepted and hosts resolving to private, loopback, or
link-local IP ranges are rejected.

**Abuse-resistance limits:** Time-window queries are capped by `Sync:MaxQueryWindowDays` (default `90`) and shadow-slot
push payload size is capped by `Sync:MaxShadowSlotsPerRequest` (default `500`).

## Obfuscation Pipeline

The pipeline uses a chain-of-responsibility pattern. A raw `CalendarEvent` is passed through a sequence of
`IObfuscationTransformer` implementations, each applying one rule:

- `RemoveTitleTransformer`: replaces the event title with "Busy"
- `RemoveDescriptionTransformer`: clears the event description
- `RemoveLocationTransformer`: clears the event location
- `RemoveAttendeesTransformer`: removes all attendee names and email addresses
- `RoundTimesTransformer`: rounds start times down and end times up to the nearest configured interval
- `MergeBlocksTransformer`: collapses overlapping or adjacent slots into a single block

Transformers are registered in the DI container. Additional transformers can be injected as plugins without modifying
core code.

Obfuscation behavior is configured per calendar owner through `ObfuscationProfile` rows:

- one profile for `Client` context (peer-facing `busy-slots` and outbound sync)
- one profile for `Internal` context (`merged-freebusy`)

Profiles are resolved at runtime by `ICalendarOwnerObfuscationProfileService`. Missing profiles are auto-provisioned
with secure defaults (all sensitive fields removed, rounding enabled with 15 minutes, block merging enabled).

The resulting `BusySlot` contract always contains `start`/`end` and can optionally carry `title`, `description`,
`attendeeEmails`, and `location` when those fields are not removed by the active profile.

## Error Handling & Resilience

- A failed sync with one peer instance is logged and skipped; other peers continue unaffected.
- A failed sync for one calendar owner does not stop the scheduler from processing remaining owners.
- A temporarily unreachable calendar source causes that user's sync to be skipped and retried on the next cycle.
- All sync failures are written to structured logs with enough context (user ID, peer ID, error type) to diagnose
  without re-running the operation.

## Logging

Structured logging via Serilog. All log entries carry context as queryable properties rather than embedded strings.
Sensitive data (event titles, attendee emails, tokens) must never appear in any log entry at any log level.

`ILogRedactor` is registered globally and used on error paths to scrub common secret patterns (Bearer tokens, API
keys, OAuth codes/secrets, connection string passwords) before writing logs.

## Extensibility (Plugin System)

At startup the API:

1. Loads any DLL in the `plugins/` directory alongside the executable using `AssemblyLoadContext`.
   A failed load is logged and skipped; other assemblies continue to load normally.
2. Scans all loaded assemblies (built-in and external) for classes that:
    - Implement `ICalendarSource` **and** carry `[CalendarSourcePlugin("id", "Display Name")]`
    - Implement `IObfuscationTransformerPlugin` or `IBusySlotTransformerPlugin`
3. Registers discovered types into the ASP.NET Core DI container and builds the
   `ICalendarSourceCatalog` — a queryable index keyed on stable lowercase plugin IDs.

**Calendar-source plugin identity** is driven by the `[CalendarSourcePlugin]` attribute rather than a
hardcoded provider switch. At runtime, `ICalendarSourceResolver` resolves the active adapter for a calendar
owner using this priority order: owner's saved selection → application config → first registered plugin.

**Obfuscation transformer plugins** implement `IObfuscationTransformerPlugin` (event-level) or
`IBusySlotTransformerPlugin` (slot-level). Plugins expose a stable `Id` (used in audit logs) and an `Order`
used to sort the execution pipeline. All built-in transformers follow the same contract.

All three built-in calendar adapters (`graph`, `ical`, `mock`) ship with the main solution and follow exactly
the same rules as external third-party plugins. Per-owner provider selection is persisted in the database as a
plugin ID string, so changing the selection never requires a code change. See `plugins/README.md` for the full
extension contract.
