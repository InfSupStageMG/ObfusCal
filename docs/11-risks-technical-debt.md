# 11. Risks & Technical Debt

## Risks

| ID   | Risk                                                                                               | Likelihood | Impact | Mitigation                                                                                             |
|------|----------------------------------------------------------------------------------------------------|------------|--------|--------------------------------------------------------------------------------------------------------|
| R-01 | Microsoft Graph API permissions are blocked by a client organisation's Conditional Access policies | Medium     | High   | iCal feed fallback is supported as an alternative ingestion path                                       |
| R-02 | OAuth refresh token expiry causes silent sync failures                                             | Medium     | Medium | Token expiry is detected and logged; sysadmin status endpoint surfaces expired tokens                  |
| R-03 | Plugin DLL compiled against an older version of `ObfusCal.Core` fails to load at startup           | Low        | Medium | Plugin load errors are caught and logged; the application continues without the failing plugin         |
| R-04 | A peer instance's API key is compromised                                                           | Low        | High   | Keys are revocable by the sysadmin; revoking the key immediately stops all data exchange for that peer |
| R-05 | Clock skew between instances causes busy slot windows to be misaligned                             | Low        | Low    | All timestamps are stored and transmitted as UTC; `DateTimeOffset` is used throughout                  |

## Technical Debt

| ID    | Item                                                            | Reason Accepted                                                                     | Resolution Plan                                                                             |
|-------|-----------------------------------------------------------------|-------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------|
| TD-01 | In-memory `IShadowSlotStore`                                    | Sufficient for PoC; avoids database dependency in sprint 1                          | Replace with EF Core + PostgreSQL implementation in a later sprint                          |
| TD-02 | ~~No HTTPS termination in the container~~ Resolved              | nginx sidecar added in sprint 1; TLS is now terminated at the reverse proxy         | —                                                                                           |
| TD-03 | Peer-to-peer API key validation uses a plain header check       | Full cryptographic validation is a later sprint item                                | Replace with HMAC-signed request validation                                                 |
| TD-04 | `Microsoft.Exchange.WebServices` library is in maintenance mode | No actively maintained alternative exists for on-premise Exchange; accepted for now | Monitor; migrate to Graph API hybrid mode if client infrastructure moves to Exchange Online |
| TD-05 | No audit log for obfuscation pipeline transformations           | Auditability is a non-functional requirement but deferred from sprint 1             | Add structured audit log entries per pipeline execution in a later sprint                   |
