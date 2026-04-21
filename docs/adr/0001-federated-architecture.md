# Federated Server Architecture

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings, Coach
* **Date:** 2026-04-20

## Context and problem statement

The assignment requires that no single server holds calendar data from more than one organisation's domain. Does this
mean we must use local desktop apps, or can we deploy domain-specific servers?

## Considered options

* Option 1: Local desktop app per consultant laptop (`.msi` / `.pkg`)
* Option 2: Federated model (Single Dockerized server per company domain)

## Decision outcome

We chose **Option 2 (Federated model)**: one Dockerized server instance per company domain, hosted within that company's
own network.

## Decision rationale

A per-laptop deployment fails when the consultant's laptop is offline (holidays, weekends). A domain-level server runs
continuously and handles sync regardless of individual device state. A domain-specific server does not violate the "no
central global server" constraint.

### Consequences

* **Positive:** Sync continues when consultant laptops are offline.
* **Positive:** Updates are deployed once per server, avoiding per-device rollout.
* **Negative:** Requires a persistent database and multi-user support on the server.