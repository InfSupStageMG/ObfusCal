# Incident Response Playbook

## Scope

This playbook covers security incidents that involve ObfusCal peer authentication, calendar synchronization, and admin-driven peer credential lifecycle changes.

## Primary Audit Data Source

- Dedicated security audit sink (`SecurityAudit:FilePath`) stores append-only NDJSON entries.
- Entry chain fields (`previousEntryHash`, `entryHash`) provide tamper-evidence checks.
- Required fields per event: `eventCode`, `timestampUtc`, `actorIdentity`, `targetResource`, `outcome`, `correlationId`.

## Useful Event Codes

- `AUTH_SUCCESS`
- `AUTH_FAILURE`
- `PEER_SLOT_PUSH`
- `PEER_SLOT_REJECTED`
- `CONFIG_CHANGE`
- `KEY_ROTATION`
- `KEY_REVOCATION`
- `STATUS_READ`

## Scenario 1: Suspected Credential Compromise

1. Identify suspicious events in the audit log (`AUTH_FAILURE` spikes, unexpected `AUTH_SUCCESS`).
2. Locate impacted peer in `PeerConnections` by `InstanceId`.
3. Revoke access immediately:
   - `POST /api/admin/peer-connections/{id}/revoke`
4. Rotate key only after peer owner confirms remediation:
   - `POST /api/admin/peer-connections/{id}/rotate-key`
5. Confirm no further successful authentication for the compromised actor.

Suggested SQL checks:

```sql
SELECT id, instanceid, status, revokedat, lastsyncedat, lastsyncsucceeded
FROM "PeerConnections"
WHERE instanceid = :instanceId;
```

## Scenario 2: Suspected Data Exfiltration

1. Correlate suspicious request traces via `correlationId` in audit entries.
2. Verify if pushes were accepted (`PEER_SLOT_PUSH`) or rejected (`PEER_SLOT_REJECTED`).
3. Confirm owner mapping boundaries for the peer.
4. Revoke peer access if unauthorized access is suspected.
5. Preserve the audit sink file and database snapshot before further remediation actions.

Suggested SQL checks:

```sql
SELECT pcm."CalendarOwnerId", pcm."CalendarOwnerRef", pc."InstanceId", pc."RevokedAt"
FROM "CalendarOwnerPeerMappings" pcm
JOIN "PeerConnections" pc ON pc."Id" = pcm."PeerConnectionId"
WHERE pc."InstanceId" = :instanceId;
```

## Scenario 3: Revoke a Peer's Access

1. Retrieve peer ID from admin listing endpoint:
   - `GET /api/admin/peer-connections`
2. Revoke peer:
   - `POST /api/admin/peer-connections/{id}/revoke`
3. Validate resulting audit entry (`KEY_REVOCATION`).
4. Validate subsequent peer calls fail authentication (`AUTH_FAILURE`).

## Integrity Verification (Tamper-Evidence)

- Validate line-by-line hash chaining in the audit sink:
  - Recompute hash from each entry payload plus `previousEntryHash`.
  - Confirm each entry's `entryHash` matches computed value.
  - Confirm each entry's `previousEntryHash` equals prior entry's `entryHash`.

## Evidence Collection Checklist

- Security audit sink file from affected time window
- API operational logs with matching trace IDs
- Relevant `PeerConnections` and `CalendarOwnerPeerMappings` rows
- Timeline with UTC timestamps and operator actions

