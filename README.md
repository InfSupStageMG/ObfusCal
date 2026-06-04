# ObfusCal

[![Build](https://github.com/InfSupStageMG/ObfusCal/actions/workflows/docker.yaml/badge.svg?branch=main)](https://github.com/InfSupStageMG/ObfusCal/actions/workflows/docker.yaml)
[![Docs](https://img.shields.io/badge/docs-MkDocs-blue)](https://infsupstagemg.github.io/ObfusCal/)
[![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE.md)

ObfusCal is an open-source calendar synchronisation tool that lets users stay in sync across multiple domains without
exposing sensitive information. Events from external domains appear in your calendar as obfuscated busy blocks where
metadata is filtered by per-owner, per-context obfuscation settings.

It is designed for professionals who maintain calendars in multiple organisations and need a privacy-preserving way to
keep everyone in the loop.

---

## How it works

Each organisation runs their own instance of ObfusCal within their own network. Instances exchange only obfuscated busy
slots over a secured API. Peer endpoints are expected to use HTTPS; the application rejects `http://` peer base URLs and
validates the upstream certificate chain by default. No raw event data ever crosses a domain boundary. Calendar owners
authenticate with their existing company credentials via Entra ID (Azure AD), and the system fetches their calendar
automatically on a configurable schedule. Supported calendar sources include Outlook (Microsoft 365 via Microsoft
Graph), Google Calendar, iCloud CalDAV, and read-only iCal (`.ics`) feeds.

Calendar owners can configure multiple source instances, assign an optional color to each source, and review the merged
availability view with those colors carried through the legend, event badges, and merged-event details. When no color is
configured, ObfusCal falls back to an automatic palette.

For Outlook and Google Calendar owners who enable write-back, the sync cycle also writes **ObfusCal-managed placeholder
events** into the connected calendar. These placeholders contain only the configured title plus start/end time, are
tagged with provider metadata for safe cleanup, and never include peer identity, attendee lists, locations, or event
content.

Peer sync traffic is also rate limited per authenticated peer, with an IP-based backstop for unauthenticated requests.
The shadow-slot push and busy-slot pull endpoints each use their own configurable window, and API request bodies are
capped at 1 MB by default to reduce DoS risk.

---

## Documentation

- [Home](https://infsupstagemg.github.io/ObfusCal/)
- [ICloud CalDAV setup guide](https://infsupstagemg.github.io/ObfusCal/icloud-caldav-setup/)
- [Contributing guide](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security policy](.github/SECURITY.md)
- [Changelog](CHANGELOG.md)

---

## Community & project policies

- Contributions are welcome through issues and pull requests; start with `CONTRIBUTING.md`.
- Community expectations are documented in `CODE_OF_CONDUCT.md`.
- Security vulnerabilities should be reported privately through `.github/SECURITY.md`.
- Review ownership for sensitive or security-relevant areas is documented in `.github/CODEOWNERS`.

---

## Project structure

<!-- START_TREE path="." max_depth="2" -->

```text
ObfusCal/
├── ObfusCal.Api/                      # ASP.NET Core entry point, controllers, DI composition root
│   ├── Authentication/
│   ├── Authorization/
│   ├── Components/
│   ├── Controllers/
│   ├── Properties/
│   ├── RateLimiting/
│   ├── wwwroot/
│   ├── DotEnvLoader.cs
│   ├── ObfusCal.Api.csproj
│   ├── ObfusCal.Api.http
│   ├── Program.cs
│   ├── ProgramSetup.cs
│   ├── SecurityHeadersMiddleware.cs
│   ├── appsettings.Development.json
│   └── appsettings.json
├── ObfusCal.Application/              # Use cases (CQRS), interfaces, obfuscation pipeline
│   ├── Calendars/
│   ├── Configuration/
│   ├── Interfaces/
│   ├── Obfuscation/
│   ├── UseCases/
│   ├── DependencyInjection.cs
│   ├── ObfusCal.Application.csproj
│   └── PluginDiscovery.cs
├── ObfusCal.Domain/                   # Core business rules, domain models, obfuscation transformers
│   ├── Models/
│   ├── Obfuscation/
│   └── ObfusCal.Domain.csproj
├── ObfusCal.Infrastructure/           # Calendar adapters, EF Core persistence, storage implementations
│   ├── Calendars/
│   ├── Persistence/
│   ├── Security/
│   ├── Storage/
│   ├── Sync/
│   ├── DependencyInjection.cs
│   └── ObfusCal.Infrastructure.csproj
├── ObfusCal.Plugins.GoogleCalendar/   # Google Calendar source plugin (built alongside Api, output to plugins/)
│   ├── GoogleCalendarSourcePlugin.cs
│   └── ObfusCal.Plugins.GoogleCalendar.csproj
├── ObfusCal.Plugins.ICloudCalendar/   # iCloud CalDAV source plugin (built alongside Api, output to plugins/)
│   ├── ICloudCalendarSourcePlugin.cs
│   └── ObfusCal.Plugins.ICloudCalendar.csproj
├── ObfusCal.Tests/                    # Unit and integration tests
│   ├── Helpers/
│   ├── Integration/
│   ├── Unit/
│   └── ObfusCal.Tests.csproj
├── docs/                              # arc42 architecture documentation (served via MkDocs)
│   ├── adr/
│   ├── img/
│   ├── sbom/
│   ├── 01-introduction-goals.md
│   ├── 02-architecture-constraints.md
│   ├── 03-context-scope.md
│   ├── 04-solution-strategy.md
│   ├── 05-building-block-view.md
│   ├── 06-runtime-view.md
│   ├── 07-deployment-view.md
│   ├── 08-cross-cutting-concepts.md
│   ├── 10-quality-requirements.md
│   ├── 11-risks-technical-debt.md
│   ├── 12-glossary.md
│   ├── clean-architecture.md
│   ├── icloud-caldav-setup.md
│   ├── incident-response.md
│   ├── index.md
│   ├── security-alerting-template.md
│   └── test-catalog.md
├── downloaded/
│   ├── test-catalog.md
│   ├── test-results.html
│   └── test-results.trx
├── plugins/                           # Plugin DLL drop folder scanned at startup
│   └── README.md
├── .dockerignore
├── .editorconfig
├── .env.example
├── .gitignore
├── .gitmessage
├── AGENTS.md
├── CHANGELOG.md
├── CODE_OF_CONDUCT.md
├── CONTRIBUTING.md
├── Directory.Build.props
├── Dockerfile
├── LICENSE.md
├── ObfusCal.slnx
├── README.md
├── build.log
├── docker-compose.yaml
├── dotnet-tools.json
├── global.json
├── mkdocs.yml
├── nginx.conf
├── renovate.json
└── stryker-config.json
```

<!-- END_TREE -->

### Layer dependencies

```
ObfusCal.Api
├── ObfusCal.Application
│   └── ObfusCal.Domain
└── ObfusCal.Infrastructure
    ├── ObfusCal.Application
    └── ObfusCal.Domain
```

`ObfusCal.Domain` has zero external dependencies. Only the composition root in `Program.cs` contains feature code
that bridges `ObfusCal.Api` and `ObfusCal.Infrastructure`.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Podman](https://podman.io/) or [Docker](https://www.docker.com/products/docker-desktop) for containerised runs
- [OpenSSL](https://openssl-library.org/source/) for local certificate generation

---

## Running locally

### Option 1 (recommended): Compose stack (API + PostgreSQL + reverse proxy)

1. Create local certificates (see `certs/README.md`):

```powershell
New-Item -ItemType Directory -Force -Path certs\nginx | Out-Null
New-Item -ItemType Directory -Force -Path certs\api  | Out-Null
openssl req -x509 -nodes -days 365 -newkey rsa:2048 `
  -keyout certs\nginx\tls.key -out certs\nginx\tls.crt `
  -subj "/CN=obfuscal.local"
openssl pkcs12 -export `
  -out certs\api\api.pfx `
  -inkey certs\nginx\tls.key `
  -in certs\nginx\tls.crt `
  -passout pass:your_cert_password
```

2. Create a `.env` file from `.env.example` and fill in the values.

   Browser sign-in for the Blazor UI now uses Entra ID OpenID Connect on the server side, so the Entra app registration
   must include a web redirect URI for `https://localhost:7001/signin-oidc` (and any reverse-proxy hostnames you use)
   and you must provide `AZUREAD__CLIENTSECRET`.

   Outlook consent via Microsoft Graph now supports two access modes:
    - read-only: `GraphConsent:ReadOnlyScope=https://graph.microsoft.com/Calendars.Read offline_access`
    - write-back: `GraphConsent:ReadWriteScope=https://graph.microsoft.com/Calendars.ReadWrite offline_access`

   In the Blazor add-calendar wizard, Outlook source instances now ask the user to choose which of these access
   levels should be requested before the sign-in redirect starts.

   Source instances connected with read-only consent can sync events but cannot write ObfusCal-managed placeholders.

   Google Calendar consent now requires
   `GoogleConsent:Scope=https://www.googleapis.com/auth/calendar.events`
   so ObfusCal can both read the owner's calendar events and maintain ObfusCal-managed write-back placeholders.

   On the first successful browser sign-in, ObfusCal automatically provisions a `CalendarOwner` record keyed by the
   user's Entra object ID. After you are signed in, the header also exposes a **Switch user** action that reopens the
   Entra account picker without requiring a manual sign-out first.

   The Blazor UI is role-aware:

    - normal users see the dashboard, their own calendar-owner settings, and a read-only **My Peers** view for their
      peer request / approval status
    - sysadmins see global **Calendar Owners**, **Peer Connections**, **Sync Status**, and **Health Status** views

   For Google Calendar OAuth, set `GOOGLECONSENT__REDIRECTURI` to a Google-registered callback such as
   `https://localhost/consent-callback` or a public HTTPS URI. Do not use `https://obfuscal.local/consent-callback`
   for Google OAuth; Google rejects `.local` redirect domains.

3. Optionally add a hosts entry for `obfuscal.local`:

```
127.0.0.1 obfuscal.local
```

4. Start the stack:

```powershell
# Podman
podman compose up -d --build

# Docker
docker compose up -d --build
```

5. Verify:
    - `https://obfuscal.local/health`
    - `https://obfuscal.local/swagger` (Development mode only)
    - `http://obfuscal.local` redirects to HTTPS
    - API runs with hardened container defaults (`read_only`, `cap_drop: [ALL]`, `no-new-privileges`, `tmpfs: /tmp`)

   Optional API container sizing can be set via `.env`:

    - `API_MEM_LIMIT` (default `512m`)
    - `API_CPUS` (default `4.0`)

### Option 2: Run API with .NET CLI (requires PostgreSQL first)

`ObfusCal.Api` applies EF Core migrations on startup, so `dotnet run` needs a reachable PostgreSQL instance.

1. Start only the database service:

```powershell
# Podman
podman compose up -d db

# Docker
docker compose up -d db
```

2. Run the API:

```powershell
dotnet run --project ObfusCal.Api
```

3. Open `https://localhost:7001/swagger`.

   For local OAuth testing:

    - use `https://localhost:7001/` to exercise browser SSO for the Blazor UI (`/signin-oidc` callback)
    - use `https://localhost:7001/swagger` to exercise bearer-token API testing in Swagger (
      `/swagger/oauth2-redirect.html` callback)

   After browser sign-in completes, the app will auto-create the matching calendar owner if it does not already exist.

> **Important Note for Open-Source Users:** While ObfusCal can synchronize calendar events from multiple providers (
> Google, iCloud, generic iCal), the **Administration Dashboard currently requires a Microsoft Entra ID (Azure AD)
> tenant
** for user sign-in. Generic OIDC providers (e.g., Keycloak, Auth0) are not yet supported for the admin UI.

---

## iCloud setup quick guide

If you configure iCloud as a calendar source, you need:

- Your Apple ID email
- An Apple app-specific password
- The full iCloud CalDAV calendar URL

For a full walkthrough, see `docs/icloud-caldav-setup.md`.

### 1) Generate an app-specific password

1. Open `https://account.apple.com/account/manage`.
2. Sign in, then go to the **App-Specific Passwords** section.
3. Create a new app-specific password for ObfusCal.
4. Copy it immediately and store it safely.

Important: Apple only shows this password once. You cannot view the same password again later.

### 2) Find the iCloud CalDAV URL parts

1. Open `https://www.icloud.com/calendar/`.
2. Open browser DevTools -> **Network** tab.
3. Filter/search for `collections`.
4. In the calendar UI, deselect/reselect the target calendar to trigger requests.
5. From matching requests, collect:
    - The `p` shard code from the host (for example `p123` from `p123-caldav.icloud.com`)
    - The `dsid` segment in the URL path
    - The calendar identifier in the path (GUID-like, for example `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`)

### 3) Build the URL used by ObfusCal

Use:

`https://p***-caldav.icloud.com/<dsid>/calendars/<calendar-id>/`

Then configure ObfusCal with:

- Calendar URL: the URL above
- Apple ID: your Apple ID email
- App-specific password: the password from step 1

---

## Development

Build:

```powershell
dotnet build
```

Run all tests:

```powershell
dotnet test
```

> Integration tests require Docker or Podman to be running; they spin up an ephemeral PostgreSQL container via
> Testcontainers.

### CI/CD Test Reports

Every push or pull request triggers an automated test run via GitHub Actions.
To view test results:

1. Navigate to the **Actions** tab in GitHub and select the workflow run.
2. A high-level **Test Results Summary** is rendered natively on the summary page.
3. Detailed `.trx` (Visual Studio Test Results) files are stored as **Artifacts** at the bottom of the summary page and
   are retained for 14 days. You can download these and open them in Visual Studio or Rider for deeper analysis of
   failed tests.

### Release versioning

ObfusCal derives its application version from Git tags via `MinVer`.

- Create release tags in the form `vX.Y.Z` (for example `v1.2.0`).
- CI/CD uses the latest reachable matching tag to calculate the semantic version for builds and published images.
- If no valid `vX.Y.Z` tag exists yet, builds stay on the `1.0` line as prereleases (for example
  `1.0.0-alpha.0.<height>`).
- The Docker publishing workflow stamps the computed version into the .NET assemblies and publishes GHCR image tags for
  the semantic version, the same version with the `v` prefix, `latest`, and a short commit-SHA tag.
- The running application shows its current version in the Blazor footer and on the Dashboard so operators can confirm
  what build is deployed.

For repeatable deployments, prefer pinning server environments to a semantic-version image tag instead of relying on
`latest`.

## Secret management

ObfusCal now uses a central abstraction (`ISecretProvider`) for secret access and validates required secrets during
startup.

- Default provider: `EnvironmentSecretProvider` (reads environment variables first, then configuration)
- Optional provider mode: `ExternalSecretProvider` stub (selected via `Secrets:Provider=External`)
- Startup fail-fast: app startup stops when required secrets are missing
- Log safety: `ILogRedactor` masks known sensitive patterns (bearer tokens, api keys, OAuth secrets/codes,
  connection-string passwords) before they are logged
- Security audit trail: `ISecurityAuditService` writes append-only NDJSON audit events to a dedicated sink
  (`SecurityAudit:FilePath`) with hash chaining (`previousEntryHash` -> `entryHash`) for tamper-evidence
  - When `SecurityAudit:FilePath` is a full filename, ObfusCal writes to that exact file.
  - When it ends with a directory separator (for example `/tmp/SecurityAudit/` in the container), ObfusCal creates
    `security-audit.ndjson` inside that directory.

Environment variable names use the standard double-underscore mapping (for example `GRAPHCONSENT__CLIENTSECRET` and
`CONNECTIONSTRINGS__DEFAULTCONNECTION`).

Outlook consent via Microsoft Graph supports split scopes by default:

- read-only: `https://graph.microsoft.com/Calendars.Read offline_access`
- write-back: `https://graph.microsoft.com/Calendars.ReadWrite offline_access`

The add-calendar flow surfaces that choice explicitly for Outlook source instances instead of silently picking one of
the provider actions.

`GraphConsent:AuthorityTenant` can be set to `common` or `consumers` as groundwork for personal-account consent
scenarios. Leave it unset to use `AzureAd:TenantId`.

The Google consent scope defaults to `https://www.googleapis.com/auth/calendar.events`. Override it only if your
Google OAuth client intentionally uses an equivalent write-capable Calendar Events scope.

For browser SSO, `AZUREAD__CLIENTSECRET` is required at startup together with `AZUREAD__TENANTID` and
`AZUREAD__CLIENTID`. The Entra app registration must allow both the server-side web callback (`/signin-oidc`) and any
Swagger OAuth redirect URI you configure.

## HTTP security defaults

- Production uses HSTS + HTTPS redirection.
- API/UI responses include `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and
  `Referrer-Policy: no-referrer`.
- Cookie policy is enforced with `HttpOnly=Always`, `Secure=Always`, and `SameSite=Lax` defaults.

For Google Calendar OAuth, you can optionally override the callback URI with `GOOGLECONSENT__REDIRECTURI`. This must
match a redirect URI registered on the Google OAuth client exactly.

### Required secrets at startup

- `ConnectionStrings:DefaultConnection`
- `AzureAd:TenantId`
- `AzureAd:ClientId`
- `GraphConsent:ClientId`
- `ColumnEncryption:Key` - a 256-bit AES key used to encrypt sensitive database columns at rest

**Generating the column encryption key**

The application will refuse to start if this key is missing. Generate it once per instance and store it in your `.env`
file (or secret store). Never reuse the same key across instances and never rotate it without re-encrypting existing
data first.

```powershell
# PowerShell 7
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

```bash
# bash / openssl
openssl rand -base64 32
```

Add the output to `.env`:

```
COLUMNENCRYPTION__KEY="<paste key here>"
```

> **If this key is lost**, all encrypted column values (peer API key hashes, calendar source credentials) become
> unreadable and must be re-entered. Back it up alongside your DataProtection keys.

Use `.env.example` as the authoritative placeholder list for local/compose configuration.

### Peer sync hardening

- Peer sync requests are rate limited by authenticated peer ID, with an IP-based backstop for unauthenticated API
  requests.
- `POST /api/shadow-slots` and `GET /api/sync/busy-slots/{calendarOwnerRef}` each have their own configurable
  window/permit settings.
- API request bodies are capped at 1 MB by default via `Sync__MaxRequestBodySizeBytes`.
- `Sync__WriteBackLookAheadDays` controls how far ahead ObfusCal queries and reconciles provider-managed placeholders
  during a sync cycle (default `90`).
- `Sync__WriteBackPlaceholderTitle` provides the fallback title for write-back placeholders when a calendar owner has
  not set a custom title in the UI (default `Busy`).
- Security audit events are emitted with a stable taxonomy including `AUTH_SUCCESS`, `AUTH_FAILURE`,
  `PEER_SLOT_PUSH`, `PEER_SLOT_REJECTED`, `CONFIG_CHANGE`, `KEY_ROTATION`, and `KEY_REVOCATION`.
- Alert templates are documented in `docs/security-alerting-template.md`.
- Incident handling runbooks are documented in `docs/incident-response.md`.
- Per-owner write-back remains opt-in through the **Calendar Write-Back** section in `CalendarOwnerDetail`; turning the
  flag off stops future placeholder reconciliation without deleting existing managed events immediately.

## Sysadmin peer approval

- ObfusCal uses an Entra ID app role named `Sysadmin` on the API app registration.
- Only users assigned this role can call `/api/admin/peer-connections` endpoints.
- `POST /api/admin/peer-connections/{id}/approve` generates a cryptographically secure API key and returns it once.
- The same approval call can also store a `PinnedCertificateThumbprint` and an optional `ClientCertificateThumbprint`.
- Certificate pins should be entered as the leaf certificate thumbprint shown by the operating system or certificate
  tool.
- Peer API keys are stored as salted PBKDF2-SHA256 hashes in `PeerConnections.ApiKeyHash` (`210000` iterations).
- `POST /api/admin/peer-connections/{id}/rotate-key` rotates the key atomically and invalidates the previous key
  immediately.
- `POST /api/admin/peer-connections/{id}/revoke` sets `PeerConnections.RevokedAt` and blocks peer authentication
  immediately.
- Peer endpoints enforce scope claims from `PeerConnections.Scopes` (`push_shadow_slots`, `pull_busy_slots`).
- Peer sync requests include `X-Peer-Timestamp` and are rejected when outside
  `Sync:PeerRequestTimestampToleranceSeconds` (default 300 seconds).
- `POST /api/admin/peer-connections/{id}/suspend` sets the peer to `Suspended` and sync/auth traffic for that peer is
  blocked.

### Peer transport security setup

1. **Production / staging with CA-issued certificates**
    - Keep `PeerTransportSecurity:AllowSelfSignedCerts=false`.
    - Use a public or privately trusted certificate on each peer endpoint.
    - Optionally populate `PinnedCertificateThumbprint` to pin the exact leaf certificate.
    - If you rotate the certificate, update the pin in the same maintenance window.

2. **Development with self-signed certificates**
    - Set `PeerTransportSecurity:AllowSelfSignedCerts=true`.
    - Use the local certificate instructions in `certs/README.md`.
    - Start the compose stack; the API logs a warning whenever self-signed peer certificates are allowed.

3. **Optional mTLS groundwork**
    - Import the client certificate into the machine or user certificate store that hosts ObfusCal.
    - Set `ClientCertificateThumbprint` on the peer record.
    - The application will look up that certificate locally and present it during the TLS handshake.
    - Certificate issuance, provisioning, and renewal remain an operations responsibility.

## Input validation and SSRF protections

- Request DTOs use DataAnnotations and invalid payloads are returned as `400` `ValidationProblemDetails`.
- iCal feed URLs and peer base URLs are validated with a shared URL safety policy:
    - only `https` scheme is accepted
    - hosts resolving to private, loopback, or link-local IP ranges are rejected
- Query windows for `busy-slots` and `merged-freebusy` are bounded by `Sync:MaxQueryWindowDays` (default `90`).
- Shadow-slot push payloads are bounded by `Sync:MaxShadowSlotsPerRequest` (default `500`).
- Calendar-owner scoped API endpoints enforce object-level ownership checks and return `404 Not Found` for cross-owner
  access attempts to prevent tenant ID enumeration.
- Peer sync owner-scoped shadow-slot pushes are accepted only when the authenticated peer is mapped to the requested
  `calendarOwnerRef`; mismatches are rejected with `403 Forbidden`.

Run mutation tests with Stryker:

```powershell
dotnet tool restore
dotnet stryker --config-file stryker-config.json
```

Mutation reports are generated under `StrykerOutput/` (HTML and JSON).
The configured mutation gate is 75% (`thresholds.low` and `thresholds.break` in `stryker-config.json`).

---

## Documentation

Public docs site: https://infsupstagemg.github.io/ObfusCal/

Architecture documentation is written in arc42 format and served via MkDocs:

```powershell
pip install mkdocs-material
mkdocs serve
```

Then open `http://localhost:8000`.

---

## Roadmap

**Sprint 1 (complete):** Project scaffolding, Docker/Podman setup, CI/CD pipeline, pluggable calendar adapter
interface, core obfuscation pipeline (strip title, description, attendees, location, round times, merge blocks),
in-memory busy slot storage, REST API with OpenAPI/Swagger, push/pull shadow slots endpoints, structured Serilog
logging, nginx reverse proxy with HTTPS, EF Core + PostgreSQL persistence.

**Sprint 2 (complete):** Entra ID login for calendar owners, per-owner data scoping, peer API key authentication,
Outlook (Microsoft Graph) OAuth consent flow and calendar fetch, configurable periodic re-sync scheduler, per-owner
configurable obfuscation rules, obfuscation audit log, iCal feed import, outbound/inbound peer sync transport, sync
resilience and per-peer isolation, status and health endpoint, manual sync trigger, Blazor Server web UI with FluentUI,
mutation testing setup (Stryker), test coverage improvements, Plugin architecture end-to-end.

**Sprint 3 (complete):** Google Calendar plugin, iCloud CalDAV plugin, centralised secrets and log redaction, end-to-end
peer trust hardening, input validation and SSRF protection, CI/CD and dependency security automation, Centralized
Secrets and Cryptographic Key Management, peer connection request and fallback, sysadmin peer approval and credential
configuration.

**Sprint 4 (complete):** API authorisation and tenant boundary enforcement, data protection and privacy controls,
rate limiting and sync endpoint DoS protection, plugin supply-chain hardening, container and host runtime hardening,
inter-peer transport security, dashboard calendar view, Entra ID tenant integration and sysadmin role, automated
deployment to host server, Proton Calendar plugin, two-way sync, security logging and auditing.

**Sprint 5 (complete):** Calendar week start on Monday, write-back that respects obfuscation rules, clearer
obfuscation profile labels, sync progress indicator, UX/UI usability improvements, full-day event display and UTC
handling fixes, source-label propagation through the pipeline, migration fixes, and iCloud sync bug fixes.

**Sprint 6 (ongoing):** Security review preparation and auth flow integration in the calendar source add wizard are
currently in progress. Semantic versioning, color updates, and calendar source editor UX improvements have been
completed in this sprint. Preparations for completing the project and open-sourcing are underway.

---

## Contributing

ObfusCal started as an internship project and is being prepared for long-term open-source maintenance.

If you want to contribute:

1. read the [Contributing guide](CONTRIBUTING.md)
2. check the open issues for scoped work items
3. prefer issues labelled `good first issue` or `help wanted` if you are new to the project
4. follow the [PR checklist](.github/pull_request_template.md)

---

## License

GNU GPL v3.0. See [LICENSE](LICENSE.md) for details.
