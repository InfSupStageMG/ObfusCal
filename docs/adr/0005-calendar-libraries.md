# ADR 0005: Calendar integration libraries

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings
* **Date:** 2026-04-20

## Context and problem statement

The system must integrate with multiple calendar backends (Microsoft 365, Exchange on-premise, Google Workspace,
CalDAV/iCal). We needed to decide whether to use vendor SDKs or raw HTTP for each.

## Considered options

* Official SDKs where available; `HttpClient` for protocols where SDK quality is weak
* `HttpClient` for all providers (including Graph/EWS)
* Plain-text credential storage (explicitly rejected)

## Decision outcome

| Integration               | Selected                              | Alternative           |
|---------------------------|---------------------------------------|-----------------------|
| iCal parsing              | `Ical.Net`                            | Manual string parsing |
| CalDAV                    | `HttpClient` directly                 | `caldav-net`          |
| Microsoft 365             | `Microsoft.Graph` SDK                 | `HttpClient`          |
| Exchange on-premise       | `Microsoft.Exchange.WebServices`      | `HttpClient` (SOAP)   |
| Google Calendar           | `Google.Apis.Calendar.v3`             | `HttpClient`          |
| OAuth / token acquisition | `MSAL.NET`                            | `IdentityModel`       |
| Credential storage        | `Microsoft.AspNetCore.DataProtection` | Plain text (rejected) |

## Decision rationale

Official SDKs are chosen where they exist and are actively maintained, as they provide typed access, handle token
refresh, and reduce implementation risk. `HttpClient` is preferred for CalDAV because the protocol is simple enough that
a dedicated library adds more risk (poor maintenance) than value. Plain-text credential storage is explicitly rejected,
and refresh tokens are encrypted at rest via the Data Protection API.

## Consequences

* **Positive:** Vendor SDKs reduce the risk of protocol-level bugs in critical auth flows.
* **Positive:** Refresh tokens are never stored in plaintext.
* **Negative:** Microsoft.Exchange.WebServices is in maintenance mode; may need replacement if on-premise Exchange usage
  grows.
* **Negative:** Each vendor SDK introduces a separate dependency that could change licensing or be abandoned.
