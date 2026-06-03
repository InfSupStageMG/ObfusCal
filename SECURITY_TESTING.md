# Security Feature Testing

This document shows how to verify every security feature in ObfusCal using the automated test
suite. No running stack, no bearer tokens, and no manual curl commands are required.

---

## Quick start

```powershell
# Run every security-related test at once
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~Security"

# Run the full test suite (includes all security tests)
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj
```

Docker (or Podman) must be running for integration tests — Testcontainers spins up a PostgreSQL
container automatically.

---

## Coverage map

| Manual guide section | What the test proves | Test class / filter |
|---|---|---|
| 1c — Security response headers | Headers are set on every response; absent without the middleware | `SecurityHeadersMiddlewareTests`, `SecurityHeadersIntegrationTests` |
| 2a — HTTP peer base URL rejected | `http://` base URL is rejected at approve time | `AdminPeerConnectionsControllerTests` |
| 3a — HTTP iCal URL rejected | `http://` iCal feed URL returns 400 | `CalendarOwnersControllerIcalFeedsTests` |
| 3b — Private IP blocked (SSRF) | Private-IP iCal URL returns 400 | `CalendarOwnersControllerIcalFeedsTests` |
| 4a — Cross-owner returns 404 not 403 | Accessing another owner's resource returns 404 | `CalendarOwnersControllerTests` |
| 4b — Unauthenticated returns 401 | No token → 401 on every protected endpoint | All controller test classes |
| 4c — Peer scope isolation | Peer mapped to owner A cannot push for owner B | `ShadowSlotsControllerTests` |
| 5 — Peer scope restrictions | Push-only peer cannot pull; pull-only peer cannot push | `ShadowSlotsControllerTests` |
| 6a — Invalid key → 401 | Wrong API key is rejected | `ShadowSlotsControllerTests` |
| 6b — Valid key + fresh timestamp | Valid key with current timestamp → 201 | `ShadowSlotsControllerTests` |
| 6c — Replay attack blocked | Stale timestamp (10 min old) → 401 | `ShadowSlotsControllerTests` |
| 6d — Revocation takes effect | Revoked peer key → immediate 401 | `AdminPeerConnectionsControllerTests` |
| 6e — Key rotation invalidates old key | Rotated peer: old key → 401, new key → 200 | `AdminPeerConnectionsControllerTests` |
| 7a — Missing secret prevents startup | Absent required secret → `InvalidOperationException` on startup | `SecretStartupValidatorTests` |
| 7b — Credentials redacted in logs | Bearer tokens and API keys never appear verbatim in audit log | `DefaultLogRedactorTests`, `FileSecurityAuditServiceTests` |
| 7c — OAuth codes redacted | `code=` query parameter is redacted | `DefaultLogRedactorTests` |
| 8a/b — Rate limit returns 429 + Retry-After | Exceeding per-peer quota → 429 with header | `ShadowSlotsControllerTests` |
| 8c — Second peer is independent | First peer hitting limit does not affect second peer | `ShadowSlotsControllerTests` |
| 8d — Unauthenticated backstop limit | IP-based backstop triggers 429 after limit | `ShadowSlotsControllerTests` |
| 9a — API key stored as PBKDF2 hash | Stored hash starts with `PBKDF2$SHA256$`, not plaintext | `AdminPeerConnectionsControllerTests`, `PeerApiKeySecurityTests` |
| 10a — Auth failure audited | Failed auth writes `AUTH_FAILURE` event without raw credential | `ShadowSlotsControllerTests` |
| 10b — Successful push audited | Accepted push writes `PEER_SLOT_PUSH` event | `ShadowSlotsControllerTests` |
| 10c — Key rotation audited | Key rotation writes `KEY_ROTATION` event | `AdminPeerConnectionsControllerTests` |
| 10d — Tamper-evidence chain intact | `previousEntryHash` of entry N matches `entryHash` of entry N-1 | `FileSecurityAuditServiceTests` |
| 10e — Credentials not in audit | Raw credential value never appears in audit file | `ShadowSlotsControllerTests`, `FileSecurityAuditServiceTests` |

---

## Section-by-section commands

### 1c — Security response headers

```powershell
# AFTER  (protection works — headers are present)
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~SecurityHeadersMiddlewareTests"
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~SecurityHeadersIntegrationTests"

# BEFORE (without middleware — headers are absent)
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~SecurityHeadersMiddlewareTests&FullyQualifiedName~WithoutSecurityHeadersMiddleware"
```

### 2a / 3a / 3b — Transport security and SSRF (iCal and peer URLs)

```powershell
# AFTER  — HTTP and private-IP URLs are rejected
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~CalendarOwnersControllerIcalFeedsTests&FullyQualifiedName~ReturnsBadRequest_WhenFeedUrl"
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~AdminPeerConnectionsControllerTests&FullyQualifiedName~ReturnsBadRequest_WhenPeerBaseUrl"

# BEFORE — without URL validation, dangerous URLs are accepted
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~CalendarOwnersControllerIcalFeedsTests&FullyQualifiedName~WithSsrfValidationDisabled"
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~AdminPeerConnectionsControllerTests&FullyQualifiedName~WithSsrfValidationDisabled"
```

### 4a — Cross-owner access returns 404, not 403

```powershell
# AFTER  — accessing another owner's resource is silently 404 (no existence disclosure)
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~CalendarOwnersControllerTests&FullyQualifiedName~ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequests"
```

### 4b — Unauthenticated requests return 401

```powershell
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~ReturnsUnauthorized_Without"
```

### 4c / 5 — Peer scope isolation and restrictions

```powershell
# AFTER  — peer mapped to owner A cannot push for B; scope mismatches are enforced
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~ShadowSlotsControllerTests&FullyQualifiedName~Scope|WithOwnerScopedPayloadForUnmapped"
```

### 6a–6e — Peer trust hardening (key auth, replay, revocation, rotation)

```powershell
# AFTER  — all trust controls enforced
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~ShadowSlotsControllerTests&FullyQualifiedName~ApiKey|Replay|Revoked"
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~AdminPeerConnectionsControllerTests"

# BEFORE — without a bounded timestamp window, stale timestamps are accepted
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~ShadowSlotsControllerTests&FullyQualifiedName~WhenTimestampToleranceIsMaximum"
```

### 7a — Missing secret prevents startup

```powershell
# AFTER  — validator throws when a required secret is absent
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~SecretStartupValidatorTests"

# BEFORE — without calling ValidateOrThrow(), the missing secret is silently ignored
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~SecretStartupValidatorTests&FullyQualifiedName~WithoutValidation"
```

### 7b / 7c — Credential redaction in logs and audit

```powershell
# AFTER  — bearer tokens, API keys, OAuth codes are redacted
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~DefaultLogRedactorTests"
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~FileSecurityAuditServiceTests&FullyQualifiedName~Sanitizes"

# BEFORE — without a redactor, raw credential values appear in the audit log
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~FileSecurityAuditServiceTests&FullyQualifiedName~WithoutRedactor"
```

### 8a–8d — Rate limiting

```powershell
# AFTER  — per-peer and backstop limits enforced
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~ShadowSlotsControllerTests&FullyQualifiedName~RateLimit|TooManyRequests|RetryAfter|Backstop"

# BEFORE — without effective rate limits, unlimited volume is accepted
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~ShadowSlotsControllerTests&FullyQualifiedName~WhenRateLimitIsEffectivelyDisabled"
```

### 9a — API key stored as PBKDF2 hash (not plaintext)

```powershell
# AFTER  — PBKDF2 salted hash; non-deterministic across calls
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~PeerApiKeySecurityTests"
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~AdminPeerConnectionsControllerTests&FullyQualifiedName~StoresOnlySha256Hash"

# BEFORE — legacy SHA256 is deterministic and vulnerable to rainbow tables
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~PeerApiKeySecurityTests&FullyQualifiedName~Legacy"
```

### 10a–10e — Security audit logging

```powershell
# AFTER  — auth failures, pushes, and key rotations all produce audit events
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~ShadowSlotsControllerTests&FullyQualifiedName~Audit"
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~AdminPeerConnectionsControllerTests&FullyQualifiedName~Audit"

# AFTER  — tamper-evidence chain: each entry hashes the previous
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~FileSecurityAuditServiceTests&FullyQualifiedName~TamperEvidence"

# BEFORE — without the redactor, credentials appear verbatim in the audit file
dotnet test ObfusCal.Tests/ObfusCal.Tests.csproj --filter "FullyQualifiedName~FileSecurityAuditServiceTests&FullyQualifiedName~WithoutRedactor"
```

---

## Container runtime checks (requires Docker)

The following security properties are enforced at the container/infrastructure level and cannot
be verified by `dotnet test`. Run them after `docker compose up --build -d`.

### 1a — API container runs as non-root

```powershell
docker exec obfuscal-api id
# Expected: uid=1000 (not uid=0)
```

### 1b — Root filesystem is read-only

```powershell
# Write outside /tmp must fail
docker exec obfuscal-api sh -c "touch /app/canary.txt && echo FAIL || echo PASS"
# Expected: PASS

# /tmp must still be writable (tmpfs)
docker exec obfuscal-api sh -c "touch /tmp/canary.txt && echo PASS || echo FAIL"
# Expected: PASS
```

### 1d — No elevated Linux capabilities

```powershell
docker exec obfuscal-api sh -lc "cat /proc/self/status | grep Cap"
# Expected:
#   CapPrm: 0000000000000000
#   CapEff: 0000000000000000
```

### 2b — API only accepts HTTPS connections

```powershell
# Plain HTTP must not succeed (redirect or refused)
curl.exe -s -o NUL -w "%{http_code}" http://localhost/health
# Expected: not 200

# HTTPS must succeed
curl.exe -k -s -o NUL -w "%{http_code}" https://localhost/health
# Expected: 200
```

### 2c — `AllowSelfSignedCerts` is off in production

```powershell
# Development default shows the flag (expected in dev only)
docker exec obfuscal-api sh -lc "printenv | grep -i SELFSIGNED"

# In production this variable must be absent or false
if (Test-Path .env.production) {
    Select-String -Path .env.production -Pattern 'SELFSIGNED' -CaseSensitive:$false
} else {
    Write-Host "Not set — defaults to false (secure)"
}
```

### 9b — Shadow slot retention job removes expired rows

This requires a running stack with database access. Set `SYNC__SHADOWSLOTRETENTIONDAYS=0` in your
`.env`, restart the API, push a slot with a past date, wait for the background job interval, then
query the database directly:

```powershell
docker exec $(docker compose ps -q db) `
  psql -U $env:POSTGRES_USER -d $env:POSTGRES_DB `
  -c 'SELECT COUNT(*) FROM "CalendarOwnerAvailabilitySlots" WHERE "End" < NOW();'
# Expected: 0 after the retention job runs
```

