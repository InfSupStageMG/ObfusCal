# ADR 0006: Extension Model: Manual Assembly Scanning Plugin Architecture

**Status:** Accepted  
**Deciders:** Matthias Hendrickx, Gijs Pennings, Coach (Info Support)  
**Date:** 2026-04-20

## Context and Problem Statement

End users and future Info Support teams need to be able to add new calendar adapters (e.g. Google Workspace) or custom
obfuscation transformers without modifying or recompiling the core application. We needed to decide how to support this
extensibility.

## Considered Options

- Compile all implementations directly into `ObfusCal.Infrastructure` (no plugin system)
- MEF (Managed Extensibility Framework)
- Manual assembly scanning via `AssemblyLoadContext`

## Decision Outcome

We use **manual assembly scanning** via `AssemblyLoadContext` to discover plugins at startup from a `plugins/`
directory.

## Decision Rationale

Our existing interfaces (`ICalendarSource`, `IEventTransformer`) already define the correct extension contracts. Manual
scanning is simpler and more transparent than MEF, gives full control over error handling when a plugin fails to load,
and is the approach used by well-known .NET tools. MEF adds configuration complexity with limited benefit at this scale.

## Consequences

- **Positive:** New adapters or transformers can be added by dropping a DLL into `plugins/` without touching core code.
- **Positive:** Simpler to debug than MEF when a plugin fails to load.
- **Negative:** Plugin DLLs must be compiled against the same `ObfusCal.Core` version; interface versioning requires
  care.
- **Negative:** Requires documenting the plugin contract so third parties can implement it correctly.
