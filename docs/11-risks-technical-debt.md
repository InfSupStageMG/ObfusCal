# 11. Risks & Technical Debt

## Risks

| ID   | Risk                                                                                                                | Likelihood | Impact | Mitigation                                                                                          |
|------|---------------------------------------------------------------------------------------------------------------------|------------|--------|-----------------------------------------------------------------------------------------------------|
| R-01 | Microsoft Graph API permissions are blocked by a client organisation's Conditional Access policies                  | Medium     | High   | iCal feed fallback is supported as an alternative ingestion path                                    |
| R-02 | OAuth refresh token expiry causes silent sync failures                                                              | Medium     | Medium | Token expiry is detected and logged; sysadmin status endpoint surfaces expired tokens               |
| R-03 | Plugin DLL compiled against an older version of `ObfusCal.Application` / `ObfusCal.Domain` fails to load at startup | Low        | Medium | Plugin load errors are caught and logged; the application continues without the failing plugin      |
| R-04 | A peer ID is spoofed in an inbound push request                                                                     | Medium     | Medium | Only configured peer IDs are accepted today; stronger signed peer auth is tracked as technical debt |
| R-05 | Clock skew between instances causes busy slot windows to be misaligned                                              | Low        | Low    | All timestamps are stored and transmitted as UTC; `DateTimeOffset` is used throughout               |

## Technical Debt

| ID    | Item                                                                            | Reason Accepted                                                                                    | Resolution Plan                                                                                                                 |
|-------|---------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------|
| TD-01 | ~~In-memory `IShadowSlotStore`~~ Resolved                                       | Sufficient for PoC; avoids database dependency in sprint 1                                         | Replaced with `EfCoreShadowSlotStore` (PostgreSQL via EF Core); `IShadowSlotStore` is now backed by the database in production. |
| TD-02 | ~~No HTTPS termination in the container~~ Resolved                              | nginx sidecar added in sprint 1; TLS is now terminated at the reverse proxy                        | —                                                                                                                               |
| TD-03 | Peer-to-peer trust currently relies on known `X-Peer-Id` values                 | Sufficient for current PoC scope, but identity is not cryptographically proven                     | Replace with signed requests and managed peer credentials                                                                       |
| TD-04 | ~~Exchange on-premise (EWS) adapter considered but not shipped~~ Not applicable | The Exchange on-premise adapter will not be implemented; no EWS adapter is present in the codebase | —                                                                                                                               |
| TD-05 | No audit log for obfuscation pipeline transformations                           | Auditability is a non-functional requirement but deferred from sprint 1                            | Add structured audit log entries per pipeline execution in a later sprint                                                       |
