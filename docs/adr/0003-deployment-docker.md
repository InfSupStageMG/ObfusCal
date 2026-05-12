# ADR 0003: Deployment method: Docker

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings
* **Date:** 2026-04-20

## Context and problem statement

The application must be self-hostable within a company network with minimal setup. We evaluated how to package and
distribute it.

## Considered options

* Self-contained `.exe` wrapped in an `.msi` / `.pkg` installer
* Docker image via Docker Compose

## Decision outcome

We deploy as a **Docker image**, run via `docker compose up`.

## Decision rationale

A self-contained installer requires distributing and re-running an update on every device on each release. With
potentially hundreds of consultant laptops, this is not maintainable. Docker eliminates OS-specific packaging entirely
and makes updates a single image pull. `docker compose up` is sufficient for any IT department to bring up an instance.

## Consequences

* **Positive:** OS independence; no separate Mac ARM and Windows x64 builds.
* **Positive:** Updates are one `docker pull`; no per-device rollout required.
* **Positive:** Environment variables and volumes make configuration portable.
* **Negative:** Requires Docker to be installed and running on the host server.
* **Negative:** Docker Desktop has commercial licensing implications for larger organisations (Rancher Desktop is a free
  alternative).

## Related security guidance

Container and runtime hardening controls for this ADR are documented in
`docs/07-deployment-view.md` (see "Container runtime hardening").

