# ObfusCal

ObfusCal is an open-source calendar synchronisation tool that lets users stay in sync across multiple domains without
exposing sensitive information. Events from external domains appear in your calendar as obfuscated busy blocks. Those
contain only start and end times, with all titles, descriptions, attendees, and locations removed.

It is designed for consultants and professionals who maintain calendars in multiple organisations and need a
privacy-preserving way to keep everyone in the loop.

---

## How it works

Each organisation runs their own instance of ObfusCal within their own network. Instances exchange only obfuscated busy
slots over a secured API. No raw event data ever crosses a domain boundary. The obfuscated slots are written directly
into each connected calendar so that everyone's availability is visible from within their existing calendar client,
without any additional subscriptions or tooling.

---

## Project structure

```
ObfusCal/
├── ObfusCal.Api/            # ASP.NET Core web API - entry point, controllers, DI wiring
├── ObfusCal.Core/           # Domain models, interfaces, obfuscation pipeline
├── ObfusCal.Infrastructure/ # Calendar adapters, storage implementations
├── ObfusCal.Sync/           # Background sync service
├── ObfusCal.Tests/          # Unit and integration tests
├── docker-compose.yaml
├── Dockerfile
├── nginx.conf
├── certs/                   # Local TLS material (gitignored except README)
├── .dockerignore
├── .gitignore
└── ObfusCal.sln
```

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/products/docker-desktop) (optional, for containerised runs)
- [OpenSSL](https://openssl-library.org/source/) (for local reverse-proxy certificate generation)

---

## Running locally

**With the .NET CLI:**

```powershell
dotnet run --project ObfusCal.Api
```

Then open `http://localhost:5000/swagger` in your browser.

**With Docker Compose + reverse proxy (HTTPS):**

1. Create local certificates (see `certs/README.md`):

```powershell
New-Item -ItemType Directory -Force -Path certs\nginx | Out-Null
New-Item -ItemType Directory -Force -Path certs\api | Out-Null
openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout certs\nginx\tls.key -out certs\nginx\tls.crt -subj "/CN=obfuscal.local"
dotnet dev-certs https -ep certs\api\api.pfx -p "change-me"
```

2. Create a local `.env` file from `.env.example` and set `API_CERT_PASSWORD`.

3. Add a hosts entry for local internal-domain testing:

```text
127.0.0.1 obfuscal.local
```

4. Start the stack:

```powershell
docker compose up --build
```

5. Verify endpoints:
   - `https://obfuscal.local/health`
   - `https://obfuscal.local/swagger` (Development mode)
   - `http://obfuscal.local` redirects to HTTPS

---

## Development

Build the solution:

```powershell
dotnet build
```

Run all tests:

```powershell
dotnet test
```

---

## Roadmap

Sprint 1 covers the foundational layer:

- Project scaffolding and Docker setup
- CI/CD pipeline via GitHub Actions
- Pluggable calendar adapter interface
- Core obfuscation pipeline
- Busy slot storage
- REST API with OpenAPI/Swagger

Later sprints will add Microsoft 365 integration via the Graph API, persistent storage, authentication via Entra ID, and
the optional booking link feature.

---

## Contributing

This project is developed as an internship assignment at [Info Support](https://www.infosupport.com). Contributions and
feedback are welcome. Please open an issue before submitting a pull request.

---

## License

GNU GPL v3.0. See [LICENSE](LICENSE) for details.
