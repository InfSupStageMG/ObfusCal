# ADR 0013: Write-Back Authentication - Delegated User Accounts

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings
* **Date:** 2026-05-21

## Context and problem statement

The calendar write-back feature pushes obfuscated placeholder events into a calendar owner's linked calendar sources
(Google Calendar, iCloud CalDAV, Microsoft 365). To do this, ObfusCal must authenticate against each provider's API. Two
broad strategies exist for this authentication: using a centralised ObfusCal service/controller account, or using each
user's own delegated credentials. Which model should ObfusCal adopt?

## Considered options

1. **Service Account / Controller Account** - ObfusCal authenticates with a single privileged application identity
   (e.g., a Google service account with domain-wide delegation, an Azure app with application-level Graph permissions,
   or a shared iCloud account) and writes placeholder events on behalf of all users.
2. **Delegated User Tokens** - ObfusCal authenticates as each individual user using that user's own OAuth refresh
   token (Google, Microsoft) or app-specific password (iCloud) and writes placeholder events on behalf of that specific
   user only.

## Decision outcome

We chose **Option 2: Delegated User Tokens**.

ObfusCal authenticates write-back operations using each calendar owner's own credentials:

- **Google Calendar:** the user's OAuth refresh token, obtained during the initial Google authorisation flow, is stored
  encrypted and exchanged for short-lived access tokens at sync time.
- **Microsoft 365 / Graph:** the user's delegated OAuth token, obtained via the Microsoft identity consent flow, is used
  with application-level `Calendars.ReadWrite` scope granted on behalf of that specific user.
- **iCloud CalDAV:** the user's Apple ID and an app-specific password are stored encrypted and used for HTTP Basic
  authentication against p-caldav.icloud.com.

In all cases ObfusCal never holds credentials that could act across multiple users' accounts, and placeholder events
appear in the user's own calendar under their own identity.

## Decision rationale

1. **Stakeholder requirement - no forced isolation account:** Stakeholders explicitly requested that users not be
   required to create or manage a separate, isolated "ObfusCal calendar account". With delegated tokens the placeholders
   appear naturally in the user's existing calendar, owned by the user, without any extra account provisioning step.
2. **Principle of Least Privilege:** Each set of credentials can only access the single user's data for which they were
   issued. A compromised token or leaked credential affects exactly one user's calendar, not all users
   organisation-wide.
3. **No domain-wide delegation required:** Service accounts with domain-wide delegation (Google Workspace) or
   application-level Graph permissions grant extremely broad access to every mailbox in a tenant. Stakeholders were
   uncomfortable granting such permissions to a third-party system, and the delegated model avoids this entirely.
4. **Simpler provider support:** iCloud does not offer a server-to-server service-account model at all; CalDAV access is
   inherently per-user. Choosing delegated credentials produces a uniform authentication story across all three calendar
   providers.
5. **Auditability:** Because placeholder events are created with the user's own token, the events carry the user's
   identity in provider audit logs, not a generic service identity. This is consistent with privacy goals and easier to
   explain to users.

## Consequences

* **Positive:** No broad administrative permissions are ever requested or stored; the blast radius of a credential
  compromise is bounded to one user.
* **Positive:** Users do not need to create any additional account; placeholders appear in their existing calendar under
  their own name.
* **Positive:** A uniform delegated-credentials model works across Google, Microsoft, and iCloud without
  provider-specific service-account setup by administrators.
* **Negative:** ObfusCal must store per-user credentials securely (encrypted at rest via `ISecretProvider`) and handle
  token refresh and rotation for each user individually.
* **Negative:** If a user revokes consent or changes their app-specific password, write-back silently fails for that
  user until they re-authorise. Sync result logging (warning level) is the current mechanism for surfacing such
  failures.
* **Residual risk:** Delegated token storage means the persistence layer is a high-value target; this is mitigated by
  encryption at rest, short-lived access tokens, and the principle that no credential grants cross-user access.
