# ADR 0009: Web GUI hosting model: Blazor Server embedded in API container

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings, Coach (internship company)
* **Date:** 2026-04-29

## Context and problem statement

ObfusCal needs a web-based GUI that allows users to view their combined obfuscated calendar and manage all system
functions (calendar owners, peer connections, sync status). The application is deployed as a single Docker container
running on the company domain. Swagger UI alone is insufficient for demo and day-to-day use. We needed to decide how to
host the GUI.

## Considered options

* Blazor Server embedded in the existing `ObfusCal.Api` process
* Blazor WebAssembly (standalone) served by nginx or a separate static host
* Blazor Web App (.NET 10 unified model) as a separate project in the same container
* Separate SPA framework (React/Angular) with CORS to the API

## Decision outcome

We use **Blazor Server embedded in the existing `ObfusCal.Api` project**, served from the same container and origin.

## Decision rationale

* **Single container:** No additional service, Dockerfile, or orchestration complexity. The GUI publishes alongside the
  API in one `dotnet publish` step.
* **Shared authentication:** The same Azure AD / JWT middleware protects both API endpoints and Razor components — no
  token relay or CORS configuration needed.
* **Direct service access:** Razor components can inject application use-case services directly, calling use-cases
  without HTTP round-trips for the internal UI.
* **No extra infrastructure:** The existing `aspnet:10.0` runtime image already includes the Blazor Server runtime (
  SignalR hub). Only a WebSocket proxy rule in nginx is required.
* **Company domain deployment:** GUI is served from the same origin, avoiding mixed-content or cross-origin concerns
  under corporate network policies.

## Consequences

* **Positive:** Zero additional containers or build targets; same CI/CD pipeline.
* **Positive:** Authentication is shared — no separate login flow for the UI.
* **Positive:** Minimal Dockerfile change (static assets bundled automatically).
* **Negative:** Requires a persistent SignalR WebSocket connection per browser tab; nginx must proxy `/_blazor` with
  `Upgrade` headers.
* **Negative:** Not suitable for offline/PWA scenarios (acceptable — this is an always-connected corporate tool).
* **Negative:** UI latency is slightly higher than Blazor WebAssembly for each interaction (acceptable for admin/demo
  use).

