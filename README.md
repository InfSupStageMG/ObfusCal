# ObfusCal

ObfusCal is an open-source calendar synchronisation tool that lets users stay in sync across multiple domains without
exposing sensitive information. Events from external domains appear in your calendar as obfuscated busy blocks where
metadata is filtered by per-owner, per-context obfuscation settings.

It is designed for consultants and professionals who maintain calendars in multiple organisations and need a
privacy-preserving way to keep everyone in the loop.

---

## How it works

Each organisation runs their own instance of ObfusCal within their own network. Instances exchange only obfuscated busy
slots over a secured API. No raw event data ever crosses a domain boundary. Consultants authenticate with their existing
company credentials via Entra ID (Azure AD), and the system fetches their calendar automatically via the Microsoft Graph
API on a configurable schedule.

---

## Project structure

```
ObfusCal/
├── ObfusCal.Domain/         # Core business rules, domain models, obfuscation transformers
├── ObfusCal.Application/    # Use cases (CQRS), interfaces, obfuscation pipeline
├── ObfusCal.Infrastructure/ # Calendar adapters, EF Core persistence, storage implementations
├── ObfusCal.Api/            # ASP.NET Core entry point, controllers, DI composition root
├── ObfusCal.Tests/          # Unit and integration tests
├── docs/                    # arc42 architecture documentation (served via MkDocs)
├── docker-compose.yaml
├── Dockerfile
├── nginx.conf
├── certs/                   # Local TLS material (gitignored except README)
└── ObfusCal.slnx
```

### Layer dependencies

```
ObfusCal.Api
├── ObfusCal.Application
│   └── ObfusCal.Domain
└── ObfusCal.Infrastructure
    ├── ObfusCal.Application
    └── ObfusCal.Domain
```

`ObfusCal.Domain` has zero external dependencies. The composition root in `Program.cs` is the only place where
`ObfusCal.Api` and `ObfusCal.Infrastructure` meet.

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

   For Google Calendar OAuth, set `GOOGLECONSENT__REDIRECTURI` to a Google-registered callback such as
   `https://localhost/consent-callback` or a public HTTPS URI. Do not use `https://obfuscal.local/consent-callback`
   for Google OAuth — Google rejects `.local` redirect domains.

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

3. Open `http://localhost:5001/swagger`.

---

## iCloud setup quick guide

If you configure iCloud as a calendar source, you need:
- Your Apple ID email
- An Apple app-specific password
- The full iCloud CalDAV calendar URL

For a full walkthrough, see `docs/13-icloud-caldav-setup.md`.

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

> Integration tests require Docker or Podman to be running — they spin up an ephemeral PostgreSQL container via
> Testcontainers.

## Secret management

ObfusCal now uses a central abstraction (`ISecretProvider`) for secret access and validates required secrets during
startup.

- Default provider: `EnvironmentSecretProvider` (reads environment variables first, then configuration)
- Optional provider mode: `ExternalSecretProvider` stub (selected via `Secrets:Provider=External`)
- Startup fail-fast: app startup stops when required secrets are missing
- Log safety: `ILogRedactor` masks known sensitive patterns (bearer tokens, api keys, OAuth secrets/codes,
  connection-string passwords) before they are logged

Environment variable names use the standard double-underscore mapping (for example `GRAPHCONSENT__CLIENTSECRET` and
`CONNECTIONSTRINGS__DEFAULTCONNECTION`).

For Google Calendar OAuth, you can optionally override the callback URI with `GOOGLECONSENT__REDIRECTURI`. This must
match a redirect URI registered on the Google OAuth client exactly.

### Required secrets at startup

- `ConnectionStrings:DefaultConnection`
- `AzureAd:TenantId`
- `AzureAd:ClientId`
- `GraphConsent:ClientId`

Use `.env.example` as the authoritative placeholder list for local/compose configuration.

## Sysadmin peer approval

- ObfusCal uses an Entra ID app role named `Sysadmin` on the API app registration.
- Only users assigned this role can call `/api/admin/peer-connections` endpoints.
- `POST /api/admin/peer-connections/{id}/approve` generates a cryptographically secure API key and returns it once.
- Peer API keys are stored as salted PBKDF2-SHA256 hashes in `PeerConnections.ApiKeyHash` (`210000` iterations).
- `POST /api/admin/peer-connections/{id}/rotate-key` rotates the key atomically and invalidates the previous key immediately.
- `POST /api/admin/peer-connections/{id}/revoke` sets `PeerConnections.RevokedAt` and blocks peer authentication immediately.
- Peer endpoints enforce scope claims from `PeerConnections.Scopes` (`push_shadow_slots`, `pull_busy_slots`).
- Peer sync requests include `X-Peer-Timestamp` and are rejected when outside `Sync:PeerRequestTimestampToleranceSeconds` (default 300 seconds).
- `POST /api/admin/peer-connections/{id}/suspend` sets the peer to `Suspended` and sync/auth traffic for that peer is blocked.

## Input validation and SSRF protections

- Request DTOs use DataAnnotations and invalid payloads are returned as `400` `ValidationProblemDetails`.
- iCal feed URLs and peer base URLs are validated with a shared URL safety policy:
  - only `https` scheme is accepted
  - hosts resolving to private, loopback, or link-local IP ranges are rejected
- Query windows for `busy-slots` and `merged-freebusy` are bounded by `Sync:MaxQueryWindowDays` (default `90`).
- Shadow-slot push payloads are bounded by `Sync:MaxShadowSlotsPerRequest` (default `500`).

Run mutation tests with Stryker:

```powershell
dotnet tool restore
dotnet stryker --config-file stryker-config.json
```

Mutation reports are generated under `StrykerOutput/` (HTML and JSON).
The configured mutation gate is 75% (`thresholds.low` and `thresholds.break` in `stryker-config.json`).

---

## Documentation

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
logging, nginx reverse proxy with HTTPS.

**Sprint 2 (in progress):** Entra ID login, per-owner data scoping, API key authentication for peer instances,
Microsoft Graph OAuth consent flow and calendar fetch, EF Core + PostgreSQL persistence, configurable periodic
re-sync scheduler, iCal feed import, outbound/inbound peer sync transport, sync resilience.

**Later sprints:** Booking link feature, mTLS for inter-peer communication.

---

## Contributing

This project is developed as an internship assignment at [Info Support](https://www.infosupport.com). Contributions and
feedback are welcome. Please open an issue before submitting a pull request.

---

## License

GNU GPL v3.0. See [LICENSE](LICENSE) for details.
