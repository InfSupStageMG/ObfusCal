# AGENTS.md

## ObfusCal Safety Checklist

Use this checklist before declaring the project "ready":

1. **Architecture boundaries**
   - Keep interfaces in `ObfusCal.Application`.
   - Keep implementations in `ObfusCal.Infrastructure`.
   - Do not reference `ObfusCal.Infrastructure` from `ObfusCal.Application` or `ObfusCal.Domain`.
   - Keep composition/wiring in `ObfusCal.Api/Program.cs` and `ObfusCal.Infrastructure/DependencyInjection.cs`.

2. **Secrets and security**
   - Use `ISecretProvider` for secret reads in runtime services.
   - Ensure startup validation runs via `SecretStartupValidationExtensions.ValidateRequiredSecrets(...)`.
   - Ensure logging paths use `ILogRedactor` for exception/body content that may contain secrets.
   - Keep `.env.example` and `ObfusCal.Api/appsettings.Development.json` placeholder-only (no real credentials).

3. **Verification gate**
   - Run:
     - `dotnet build ObfusCal.slnx --no-incremental`
     - `dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --no-build`
   - Treat warnings that indicate architectural drift as findings.

4. **Container readiness**
   - API image is single-container deployable for the API process.
   - Runtime still needs external dependencies (database, TLS cert/key material, env secrets).
   - For true one-container full stack, document that this repo currently expects separate DB/proxy services.

5. **Docs sync**
   - Update `README.md`, `docs/07-deployment-view.md`, and `docs/08-cross-cutting-concepts.md` when security or deployment behavior changes.

6. **Feature workflow (general)**
   - Start from a failing/targeted test or a clear reproducible scenario before changing behavior.
   - Prefer small, vertical slices (API + application + infrastructure + tests) over large multi-feature commits.
   - Keep changes local to the feature area; avoid opportunistic refactors unless they remove a blocker.
   - If behavior changes, update both user-facing docs and architecture docs in the same PR.

7. **Testing expectations**
   - Add or update tests for every bug fix and behavior change.
   - Prefer unit tests for parsing/transformer logic and integration tests for controllers, persistence, and sync flows.
   - Keep tests deterministic: avoid wall-clock coupling, random ordering assumptions, and external network dependencies.
   - Before declaring done, run targeted tests first, then full solution build/test gate.

8. **API and UX consistency**
   - Keep API error responses consistent (`ProblemDetails` for server errors, clear 4xx for client errors).
   - Preserve privacy defaults in UI: never expose sensitive event fields unless explicitly required by profile/context.
   - For dashboard and date/time UX, treat ranges explicitly (`[from, to)` semantics) and keep timezone handling predictable.
   - Ensure responsive behavior for filter/forms rows; avoid controls clipping at common browser zoom levels.

9. **Data and persistence discipline**
   - Store UTC timestamps in persistence models and convert at the edges (UI/input/output).
   - Use snapshot tables (`CalendarOwnerAvailabilitySlots`, shadow slots) as read models where intended; avoid mixing live and snapshot paths implicitly.
   - When changing EF models, include migration updates and validate startup migration behavior.
   - Treat concurrency warnings/exceptions in sync paths as findings; either handle explicitly or document residual risk.

10. **Deployment and runtime operations**
    - Keep `Dockerfile` single-purpose for API runtime; document external runtime requirements (DB, certs, secrets).
    - Keep `docker-compose.yaml` aligned with documented env variables and readiness checks.
    - Do not assume DataProtection key persistence in containers; if required, configure persistent key storage explicitly.
    - Validate health endpoint behavior (`/health`) after startup/config changes.

11. **Observability and logging quality**
    - Use structured logging with stable property names for correlation (`CalendarOwnerId`, `PeerId`, `TraceId`, etc.).
    - Log enough operational context to diagnose sync and parsing failures without leaking secrets.
    - Prefer warning-level logs for recoverable per-owner/per-peer failures and continue processing remaining work.
    - Keep noisy logs bounded in loops/background jobs.

12. **Plugin and extensibility rules**
    - New calendar sources must be discovered through plugin contracts and registered via the catalog, not hardcoded switches.
    - Keep plugin IDs stable and lowercase; treat ID changes as breaking changes for persisted selections.
    - Ensure fallback behavior remains explicit when plugins are unavailable or not ready.
    - Add tests for plugin resolution order (owner selection -> configured provider -> first available).

13. **PR hygiene and review readiness**
    - Keep PRs scoped to one issue/user story with a clear validation note (what changed, how it was tested).
    - Include before/after behavior notes for bug fixes (especially timezone, sync, and obfuscation changes).
    - Avoid TODOs without linked issues and remove debug code before merge.
    - Treat analyzer warnings related to architecture drift or security as actionable findings, not cosmetic noise.

14. **Blazor composition and code-behind discipline**
    - Keep page markup in `*.razor` and move page logic into focused `*.razor.cs` partial files.
    - Prefer feature-oriented partials for large pages (for example: `.Sources`, `.Feeds`, `.ICloud`, `.Profiles`, `.Sync`, `.Models`).
    - Treat a page as an orchestrator; extract reusable UI sections into child components when a page grows beyond a single feature.
    - Keep Blazor components in the presentation layer (`ObfusCal.Api`) and depend on `ObfusCal.Application` abstractions only.
    - Do not move UI-specific view models into `ObfusCal.Application` unless they become shared contracts.
    - If a page or partial becomes difficult to review, split it before merging and document the resulting file layout in the PR note.
