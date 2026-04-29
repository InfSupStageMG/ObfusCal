# ADR 0002: Programming language: .NET 10 (C#)

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings
* **Date:** 2026-04-20

## Context and problem statement

We needed to select a backend language for the server application. The choice affects available libraries, team
productivity, and long-term maintainability within an Info Support context.

## Considered options

* .NET 10 (C#)
* Go
* TypeScript (Node.js)
* Python

## Decision outcome

We selected **.NET 10 (C#)**.

## Decision rationale

.NET has the strongest alignment with this project: deep in-house expertise at Info Support, first-class Microsoft Graph
SDK support, built-in background service hosting, and a mature ecosystem for the enterprise calendar integrations we
need.

## Consequences

* **Positive:** Team is productive immediately without a learning curve.
* **Positive:** Microsoft Graph and MSAL SDKs are officially maintained by Microsoft.
* **Positive:** `BackgroundService`, minimal APIs, and Data Protection are all built in.
* **Negative:** Heavier runtime compared to Go for a simple HTTP service.

## Pros and cons of the options

### Go

* Good, because it produces small single-binary deployables with no runtime dependency.
* Bad, because neither team member has experience with it, and enterprise calendar SDK support is limited.

### TypeScript

* Good, because both team members have experience with it.
* Bad, because the enterprise calendar library ecosystem is weaker than .NET's for this use case.

### Python

* Good, because it is fast to prototype.
* Bad, because library support for enterprise calendar APIs (Graph, EWS) is insufficient.
