# ADR 0010: UI Component Library: Microsoft Fluent UI Blazor

**Status:** Accepted
**Deciders:** Matthias Hendrickx, Gijs Pennings, Coach (Info Support)
**Date:** 2026-04-29

## Context and Problem Statement

The Blazor Server GUI needs a component library for layout, navigation, data grids, dialogs, and form inputs. The chosen
library must be free, MIT-licensed (GPL-3.0 compatible), actively maintained, unlikely to be deprecated, and safe for
deployment on a company domain under compliance scrutiny.

## Considered Options

- MudBlazor (MIT, community-maintained, Material Design)
- Microsoft Fluent UI Blazor (MIT, Microsoft-maintained, Fluent 2 design)
- Radzen Blazor (MIT free tier, small company)
- Syncfusion / Telerik / DevExpress (commercial licenses, paid)
- No library — hand-written HTML/CSS

## Decision Outcome

We use **Microsoft Fluent UI Blazor** (`Microsoft.FluentUI.AspNetCore.Components`).

## Decision Rationale

| Criterion        | Fluent UI Blazor                                         |
|------------------|----------------------------------------------------------|
| License          | MIT — compatible with our GPL-3.0 project                |
| Cost             | Free, no paid tier, no telemetry                         |
| Maintainer       | Microsoft (same vendor as .NET, Azure AD, the runtime)   |
| Deprecation risk | Very low — official Microsoft product, actively released |
| Latest release   | v4.14.1 (April 22, 2026)                                 |
| Known CVEs       | None                                                     |
| .NET 10 support  | Yes                                                      |
| Design system    | Fluent 2 — matches corporate M365 look-and-feel          |

Compared to MudBlazor (also MIT, also well-maintained), Fluent UI Blazor was chosen because:

1. **Corporate confidence:** Microsoft-backed libraries pass compliance review with the least friction. The same vendor
   provides the runtime, IDE, and identity provider we already depend on.
2. **Design consistency:** Fluent 2 aligns with the Microsoft/corporate ecosystem the application will live in on the
   company domain.
3. **Built-in components cover our needs:** `FluentDataGrid`, `FluentNavMenu`, `FluentDialog`, `FluentCard`, and
   `FluentTextField` cover calendar owner management, sync status tables, and settings — without requiring community
   extensions.

Commercial libraries (Syncfusion, Telerik, DevExpress) were excluded because they require paid procurement, add
licensing complexity, and are not MIT-compatible with GPL-3.0.

## Consequences

- **Positive:** Single NuGet dependency, ~2–3 MB added to publish output.
- **Positive:** No JavaScript framework dependencies or npm build steps.
- **Positive:** Same MIT license as the .NET runtime — no additional legal review needed beyond existing compliance.
- **Negative:** No built-in full week/month calendar scheduler — we will build a simple busy-slot timeline using
  `FluentDataGrid` or custom Razor markup with Fluent design tokens.
- **Negative:** Fluent 2 styling may look opinionated if the company uses a non-Microsoft design system (acceptable —
  company uses M365).

