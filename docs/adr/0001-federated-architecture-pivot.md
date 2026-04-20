# ADR 0001: Pivot from Local Desktop App to Federated Docker Server

**Date:** April 20, 2026  
**Status:** Accepted

## Context

Initially, the project constraint "no central server" was interpreted as avoiding *any* shared server. We planned to run
the application in the background on every consultant's laptop (Mac/Windows) via a `.msi` or `.pkg` installer,
establishing peer-to-peer communication.

## Decision

Following stakeholder feedback, we pivoted to a **Dockerized Federated Server Architecture**. The application will be
deployed as a single server instance per company domain (e.g., one server for Info Support, one for Client A).

## Consequences

* **Positive:** Background sync continues to run perfectly even when a consultant's laptop is turned off (e.g., during
  holidays).
* **Positive:** Massive improvement in maintainability. We no longer need to manually push software updates to 600+
  individual, strictly managed client laptops.
* **Positive:** Complete OS independence (avoids native macOS Apple Silicon vs. Windows compiling issues).
* **Negative:** Requires a robust persistent database (EF Core) to handle multiple users simultaneously on the server.