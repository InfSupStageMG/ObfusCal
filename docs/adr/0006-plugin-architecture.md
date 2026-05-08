# ADR 0006: Extension model: manual assembly scanning plugin architecture

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings, Coach (internship company)
* **Date:** 2026-04-20

## Context and problem statement

End users and future internship company teams need to be able to add new calendar adapters (e.g. Google Workspace) or custom
obfuscation transformers without modifying or recompiling the core application. We needed to decide how to support this
extensibility.

## Considered options

* Compile all implementations directly into `ObfusCal.Infrastructure` (no plugin system)
* MEF (Managed Extensibility Framework)
* Manual assembly scanning via `AssemblyLoadContext`

## Decision outcome

We use **manual assembly scanning** via `AssemblyLoadContext` to discover plugins at startup from a `plugins/`
directory.

## Decision rationale

Our existing interfaces (`ICalendarSource`, `IObfuscationTransformer`, `IBusySlotTransformer`) define the extension
contracts. Manual scanning is simpler and more transparent than MEF, gives full control over error handling when a
plugin fails to load, and is the approach used by well-known .NET tools. MEF adds configuration complexity with limited
benefit at this scale.

## Plugin identity

Calendar-source plugins are identified by a stable lowercase string ID declared via the
`[CalendarSourcePlugin("id", "Display Name")]` attribute on the implementing class. At startup,
`CalendarSourcePluginCatalog` scans all loaded assemblies and builds a catalog of every attributed `ICalendarSource`
type. Provider resolution and per-owner selection are then driven by this catalog rather than a hardcoded switch.

Obfuscation transformer plugins optionally implement `IObfuscationTransformerPlugin` (event-level) or
`IBusySlotTransformerPlugin` (slot-level) to expose a stable `Id` and an `Order` value used to sort the pipeline.
Types without these interfaces are also discovered and registered; they simply execute in registration order.

## Consequences

* **Positive:** New adapters or transformers can be added by dropping a DLL into `plugins/` without touching core code.
* **Positive:** Per-owner calendar-source selection is persisted as a plugin ID, which decouples persistence from any
  concrete type name.
* **Positive:** Simpler to debug than MEF when a plugin fails to load.
* **Negative:** Plugin DLLs must be compiled against the same `ObfusCal.Application` / `ObfusCal.Domain` contract
  versions; interface and attribute versioning requires care.
* **Negative:** Requires documenting the plugin contract so third parties can implement it correctly (see
  `plugins/README.md`).
