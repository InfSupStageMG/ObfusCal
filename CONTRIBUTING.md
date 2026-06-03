# Contributing to ObfusCal

Thank you for considering a contribution to ObfusCal! This document covers everything you need
to know to get started, from setting up a development environment to opening a pull request.

---

## Table of contents

- [Code of conduct](#code-of-conduct)
- [How to contribute](#how-to-contribute)
  - [Reporting bugs](#reporting-bugs)
  - [Suggesting enhancements](#suggesting-enhancements)
  - [Your first code contribution](#your-first-code-contribution)
- [Development setup](#development-setup)
- [Architecture overview](#architecture-overview)
- [Coding conventions](#coding-conventions)
- [Commit messages](#commit-messages)
- [Pull request process](#pull-request-process)
- [Testing](#testing)

---

## Code of conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By
participating you agree to uphold it. Please report unacceptable behaviour to the maintainers
via a [GitHub private security advisory](https://github.com/InfSupStageMG/ObfusCal/security/advisories/new).

---

## How to contribute

### Reporting bugs

Before filing a bug, search [open issues](https://github.com/InfSupStageMG/ObfusCal/issues)
to avoid duplicates. Then open a new issue using the **Task (Bug / Chore / Docs / Refactor)**
template and fill in the reproduction steps, expected behaviour, and actual behaviour.

If you believe the bug has security implications, **do not open a public issue**. Follow the
[security policy](.github/SECURITY.md) instead.

### Suggesting enhancements

Open an issue using the **User Story / Feature** template. Describe the problem the feature
solves, who benefits, and any design constraints you are aware of.

### Your first code contribution

Issues labelled [`good first issue`](https://github.com/InfSupStageMG/ObfusCal/labels/good%20first%20issue)
are a good starting point. Comment on the issue to let others know you are working on it, then
open a pull request once you have something to show.

---

## Development setup

### Prerequisites

| Tool | Minimum version |
|------|----------------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10 |
| [Docker](https://www.docker.com/products/docker-desktop) or [Podman](https://podman.io/) | any recent |
| [OpenSSL](https://openssl-library.org/source/) | any recent |

### Run the full stack locally

1. **Generate local TLS certificates** (see `certs/README.md` for details):

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

2. **Create a `.env` file** from `.env.example` and fill in the placeholder values. You will
   need an Azure AD app registration with a web redirect URI for
   `https://localhost:7001/signin-oidc`.

3. **Start the compose stack**:

   ```bash
   docker compose up
   ```

   The API and Blazor UI are served at `https://localhost:7001`.

### Run tests

```bash
dotnet restore
dotnet build ObfusCal.slnx --no-incremental
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj
```

Integration tests use [Testcontainers](https://testcontainers.com/) and require a running
Docker or Podman daemon.

### Run mutation tests

```bash
dotnet tool restore
dotnet stryker --config-file stryker-config.json
```

Reports are written to `StrykerOutput/`. The configured threshold is 75 % (`thresholds.low`).

---

## Architecture overview

ObfusCal follows Clean Architecture with a strict layering rule:

```
ObfusCal.Api           ← entry point, Blazor UI, controllers, DI composition root
├── ObfusCal.Application   ← use cases (CQRS), interfaces, obfuscation pipeline
│   └── ObfusCal.Domain    ← core business rules and domain models (zero external dependencies)
└── ObfusCal.Infrastructure  ← calendar adapters, EF Core, storage implementations
    ├── ObfusCal.Application
    └── ObfusCal.Domain
```

Key rules:
- Interfaces live in `ObfusCal.Application`; implementations in `ObfusCal.Infrastructure`.
- `ObfusCal.Infrastructure` must never be referenced from `ObfusCal.Application` or
  `ObfusCal.Domain`.
- Composition and wiring happen only in `ObfusCal.Api/Program.cs`,
  `ObfusCal.Api/ProgramSetup.cs`, and `ObfusCal.Infrastructure/DependencyInjection.cs`.
- Calendar source plugins live in `ObfusCal.Plugins.*` and are loaded at startup via the plugin
  catalog.

Full architecture documentation (arc42) is served at
[https://infsupstagemg.github.io/ObfusCal/](https://infsupstagemg.github.io/ObfusCal/) and
maintained under `docs/`.

---

## Coding conventions

### General

- No raw credential or secret reads outside of `ISecretProvider`.
- Use `ILogRedactor` wherever exception messages or request bodies might contain sensitive
  calendar data.
- All timestamps are stored and transmitted as UTC; use `DateTimeOffset` throughout.
- Keep `dotnet build` output warning-free; treat architecture-drift warnings as findings.
- Do not add `TODO` comments without a linked GitHub issue.
- Remove debug code before opening a PR.

### Comments

- Default to no comments. Let well-named types and methods speak for themselves.
- Add inline comments only to explain *why*, not *what*.
- Use `/// <summary>` on classes for high-level context; avoid it on methods that merely
  restate the method name.

### Blazor

- Keep page markup in `*.razor` and page logic in matching `*.razor.cs` partial files.
- `*.razor.cs` is for component code-behind only. Shared helpers belong in plain `*.cs` files
  in the appropriate `Components/` subfolder.

---

## Commit messages

Follow the [Conventional Commits](https://www.conventionalcommits.org/) style configured in
`.gitmessage`:

```
type(scope): short summary

# Types: feat | fix | chore | docs | refactor | test | ci
# Examples:
#   feat(api): add peer revocation endpoint
#   fix(sync): handle null refresh token on expiry
#   chore: update test catalog
```

- Use the imperative mood ("add", not "added" or "adds").
- Keep the subject line under 72 characters.
- Reference issues in the body or footer: `Closes #123`.

---

## Pull request process

1. **Open an issue first** (or comment on an existing one) so the scope is agreed before you
   invest time coding.
2. Fork the repository and create a feature branch: `git checkout -b 123-short-description`.
3. Make your changes following the coding conventions above.
4. Ensure `dotnet build` produces zero errors and zero warnings.
5. Ensure all existing tests pass and add new tests for any behaviour change.
6. Open a pull request against `main`. Fill in the PR template completely.
7. At least one maintainer review and approval is required before merging.
8. Squash-merge or rebase-merge; do not leave merge commits from feature branches.

---

## Testing

| Layer | Framework | Notes |
|-------|-----------|-------|
| Unit | xUnit | For parsing, transformer, and domain logic |
| Integration | xUnit + Testcontainers PostgreSQL | For controllers, persistence, and sync flows |
| Mutation | Stryker.NET | Run before opening a PR for logic-heavy changes |

- Add or update tests for every bug fix and behaviour change.
- Keep tests deterministic: no wall-clock coupling, no random ordering assumptions, no external
  network calls.
- Prefer unit tests for obfuscation pipeline logic and integration tests for repository and
  sync adapter layers.
