# System Architecture

## The Federated Model

The most significant architectural constraint on the project is the requirement that **no single server may hold
calendar data from more than one organization's domain.**

This constraint rules out a single globally hosted SaaS. Instead, ObfusCal utilizes a **Federated Architecture**:

1. Each participating organization runs its own independent instance of the application via Docker.
2. Cross-domain availability exchange happens exclusively through a documented REST API over HTTPS.
3. When Instance A needs to know that a consultant is busy in Domain B, it calls Domain B's API.
4. The API returns only pre-obfuscated busy slots. Raw data never travels across the boundary.

## The Fallback (ICS)

For highly restrictive client environments (e.g., banks) that refuse to host an ObfusCal peer instance, the system
supports a read-only `.ics` URL fallback. The Info Support instance can ingest these obfuscated feeds to ensure
availability remains accurate without requiring two-way API trust.