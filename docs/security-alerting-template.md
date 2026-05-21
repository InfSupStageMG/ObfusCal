# Security Alerting Template

This template is SIEM-agnostic and can be translated to Loki, Elasticsearch/KQL, Datadog, Splunk, or SQL-backed alert pipelines.

## Data Source

- Dedicated audit sink configured by `SecurityAudit:FilePath`
- Parse NDJSON records and index fields:
  - `timestampUtc`
  - `eventCode`
  - `actorIdentity`
  - `targetResource`
  - `targetId`
  - `outcome`
  - `correlationId`
  - `metadata.reason`

## Alert Rule 1: Repeated AUTH_FAILURE from Same Peer

- Condition:
  - `eventCode == "AUTH_FAILURE"`
  - Group by `actorIdentity`
  - Trigger when count >= 5 over rolling 10 minutes
- Severity: High
- Suggested response: Verify peer health, then revoke/rotate credentials if malicious behavior is suspected.

## Alert Rule 2: First AUTH_SUCCESS from New Peer

- Condition:
  - `eventCode == "AUTH_SUCCESS"`
  - `actorIdentity` not seen with `AUTH_SUCCESS` in prior 30 days
- Severity: Medium
- Suggested response: Validate peer onboarding approval record and expected deployment timeline.

## Alert Rule 3: Any KEY_REVOCATION

- Condition:
  - `eventCode == "KEY_REVOCATION"`
- Severity: Critical
- Suggested response: Open incident ticket immediately and verify post-revocation `AUTH_FAILURE` continuity.

## Alert Rule 4: Any CONFIG_CHANGE

- Condition:
  - `eventCode == "CONFIG_CHANGE"`
- Severity: Medium
- Suggested response: Validate initiator identity and approved change request.

## Optional Safeguard Alerts

- Hash chain break (tamper-evidence failure in `previousEntryHash`/`entryHash`) -> Critical
- Missing audit events for expected admin operations -> High

