# ADR 0003: Plugin Architecture and Testing Framework

**Date:** April 20, 2026  
**Status:** Accepted

## Context

Following a review with our coach, we evaluated how external users (or future Info Support teams) could add new calendar
adapters (e.g., Google Workspace) or custom obfuscation rules without modifying the core ObfusCal codebase. We also
needed to align our testing framework with Info Support standards.

## Decision: Plugin Architecture

We will use a **Manual Assembly Scanning Plugin Architecture** rather than compiling all implementations into the main
binary or using MEF.

* At startup, the API scans a local `plugins/` directory.
* It uses `AssemblyLoadContext` to find DLLs containing classes that implement `ICalendarSource` or `IEventTransformer`.
* It automatically registers these in the ASP.NET Core Dependency Injection container.

## Decision: Testing Framework

We are using **`MSTest`** to align with Microsoft and company standards. Additionally, we are
adopting **`Testcontainers`** to allow future integration tests to run against real, ephemeral PostgreSQL database
containers rather than relying solely on in-memory mocks.