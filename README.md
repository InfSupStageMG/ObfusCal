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
- Only a SHA-256 hash of the generated key is stored in `PeerConnections.ApiKeyHash`.
- `POST /api/admin/peer-connections/{id}/suspend` sets the peer to `Suspended` and sync/auth traffic for that peer is blocked.

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
