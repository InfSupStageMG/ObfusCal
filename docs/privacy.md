# Privacy & Security Model

The most significant privacy decision in the ObfusCal architecture is structural: **raw calendar events are never
written to the database.**

## Data Minimization Strategy

Obfuscation is not a transformation applied *before* storage — it is the gating condition *for* storage itself.

When a sync cycle runs, the calendar adapter fetches the consultant's events into memory. These objects are passed
directly to the `ObfuscationPipeline`, which applies the consultant's active rules (rounding timestamps, stripping
titles/attendees, merging blocks) to produce `BusySlot` records.

Those `BusySlot` records are what get written to the database. The raw `CalendarEvent` objects immediately fall out of
scope and are garbage collected. A database breach or an overly permissive query exposes nothing more than anonymous
time ranges.

| Data Type                      | Persisted to DB | In Memory during sync | Sent to peers |
|:-------------------------------|:----------------|:----------------------|:--------------|
| **Raw meeting details**        | Never           | During pipeline       | Never         |
| **Obfuscated BusySlot**        | Yes             | Yes                   | Yes           |
| **Consultant ID & sync state** | Yes             | Yes                   | Never         |
| **Obfuscation rules**          | Yes             | Yes                   | Never         |

## Machine-to-Machine Security

Cross-domain synchronization happens exclusively over HTTPS. Inter-server communication is secured using **API Keys**.
When a Sysadmin approves a new `PeerConnection` between Info Support and a client domain, a cryptographically secure key
is generated to guarantee data originates from a trusted organization.

## Token Storage

To sync calendars in the background, ObfusCal holds Microsoft Graph API Refresh Tokens. These tokens are **encrypted at
rest** using the .NET Data Protection API (DPAPI) or Azure Key Vault before being stored in the database.