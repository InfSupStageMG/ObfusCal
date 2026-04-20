# ObfusCal

ObfusCal is an open-source calendar synchronisation tool that lets users stay in sync across multiple domains without exposing sensitive information. Events from external domains appear in your calendar as obfuscated busy blocks — start and end times only, with all titles, descriptions, attendees, and locations stripped.

It is designed for consultants and professionals who maintain calendars in multiple organisations and need a privacy-preserving way to keep everyone in the loop.

---

## How it works

Each organisation runs their own instance of ObfusCal within their own network. Instances exchange only obfuscated busy slots over a secured API — no raw event data ever crosses a domain boundary. The obfuscated slots are written directly into each connected calendar so that everyone's availability is visible from within their existing calendar client, without any additional subscriptions or tooling.

---

## Project structure

```
ObfusCal/
├── ObfusCal.Api/            # ASP.NET Core web API — entry point, controllers, DI wiring
├── ObfusCal.Core/           # Domain models, interfaces, obfuscation pipeline
├── ObfusCal.Infrastructure/ # Calendar adapters, storage implementations
├── ObfusCal.Sync/           # Background sync service
├── Dockerfile
├── .dockerignore
├── .gitignore
└── ObfusCal.sln
```

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/products/docker-desktop) (optional, for containerised runs)

---

## Running locally

**With the .NET CLI:**

```bash
dotnet run --project ObfusCal.Api
```

Then open `http://localhost:5000/swagger` in your browser.

**With Docker:**

```bash
docker build -t obfuscal-api .
docker run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development -e ASPNETCORE_HTTP_PORTS=8080 obfuscal-api
```

Then open `http://localhost:8080/swagger` in your browser.

---

## Development

Build the solution:

```bash
dotnet build
```

Run all tests:

```bash
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

Later sprints will add Microsoft 365 integration via the Graph API, persistent storage, authentication via Entra ID, and the optional booking link feature.

---

## Contributing

This project is developed as an internship assignment at [Info Support](https://www.infosupport.com). Contributions and feedback are welcome. Please open an issue before submitting a pull request.

---

## License

MIT