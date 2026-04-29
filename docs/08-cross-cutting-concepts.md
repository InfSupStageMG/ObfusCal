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

**Machine-to-machine authentication:** Peer sync endpoints require `Authorization: ApiKey ...`. Incoming credentials
are verified against hashed `PeerConnection.ApiKeyHash` values, and outbound pushes include both `Authorization`
and `X-Peer-Id` headers so the receiving instance can authenticate and correlate the sender without trusting the
peer ID header by itself.

**Credential storage:** Microsoft Graph OAuth refresh tokens are encrypted at rest using the .NET Data Protection API (
DPAPI) before being written to the database. A database breach yields only ciphertext.

**Data scoping:** After authentication, the user's Entra ID Object ID is extracted from the JWT token and used as a
strict data boundary at the repository layer. A user can only access their own events, slots, and configuration.

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

## Extensibility (Plugin System)

At startup, the API scans a `plugins/` directory and uses `AssemblyLoadContext` to load any DLLs containing classes that
implement `ICalendarSource`. These are registered into the ASP.NET Core DI container automatically. This allows new
calendar adapters to be delivered without modifying or recompiling the core application.
