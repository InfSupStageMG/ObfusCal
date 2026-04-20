# ADR 0002: Core Technology Stack and Library Selection

**Date:** April 20, 2026  
**Status:** Accepted

## Context

We evaluated several programming languages (Go, TypeScript, Python, .NET) and libraries to build the backend server and
calendar synchronization logic.

## Decision: Language & Framework

We selected **.NET 10 (C#)**.

* *Why not Go:* Lack of existing team experience; uncertain SDK support for enterprise tools.
* *Why not TypeScript:* Sufficient experience, but lacked the performance and robust enterprise backend ecosystem of
  .NET for this specific B2B use case.
* *Why not Python:* Insufficient support for the required enterprise calendar libraries.
* *Why .NET:* Extensive in-house expertise at Info Support, exceptional MS Graph SDKs, and built-in support for
  background services and minimal APIs.

## Decision: CI/CD & Deployment

We will use **GitHub Actions** and **GitHub Container Registry (GHCR)**. Upon every push to the `main` branch, the
Docker image will automatically build and publish.

## Decision: Internal Libraries

We have selected the following libraries for our core operations:

| Category                  | Selected Library                     | Possible Alternative        | Reason / Note                                                 |
|:--------------------------|:-------------------------------------|:----------------------------|:--------------------------------------------------------------|
| **iCal Parsing**          | `Ical.Net`                           | Manual extraction           | Standardized RFC 5545 compliance.                             |
| **CalDAV Sync**           | `HttpClient`                         | `CalDav-net`                | Raw HTTP offers better control for simple XML payloads.       |
| **Microsoft 365**         | `Microsoft.Graph` SDK                | `HttpClient`                | Provides robust, typed access to Exchange Online.             |
| **MS Exchange (On-Prem)** | `MS Exchange WS`                     | `HttpClient`                | Required for legacy enterprise environments.                  |
| **Google Calendar**       | `Google.Apis.Calendar.v3`            | `HttpClient`                | Official wrapper for Workspace environments.                  |
| **OAuth**                 | `MSAL.NET`                           | Google Auth / IdentityModel | Industry standard for Azure AD / Entra ID.                    |
| **Web Server**            | ASP.NET Core Minimal APIs            | `Carter`                    | Built-in, extremely lightweight, no external dependencies.    |
| **Background Tasks**      | `Microsoft.Extensions.Hosting`       | `Quartz.net`                | Built-in `BackgroundService` is sufficient for the PoC scope. |
| **Configuration**         | `Microsoft.Extensions.Configuration` | `YamlDotNet`                | Native ASP.NET Core `appsettings.json` integration.           |
| **Credentials**           | `.NET Data Protection API`           | Plain text                  | Ensures OAuth refresh tokens are encrypted at rest.           |