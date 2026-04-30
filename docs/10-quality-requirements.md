# 10. Quality Requirements

## Quality Goals

| Priority | Quality Goal     | Motivation                                                                             |
|----------|------------------|----------------------------------------------------------------------------------------|
| 1        | Privacy          | Raw event data must never leave the originating domain or be persisted in any form     |
| 2        | Security         | All communication is authenticated; credentials are encrypted at rest                  |
| 3        | Self-hostability | A client IT department must be able to deploy an instance with minimal prerequisites   |
| 4        | Maintainability  | Calendar adapters and obfuscation rules must be swappable without modifying core logic |
| 5        | Resilience       | A failure in one peer sync must not affect sync with other peers                       |

## Quality Scenarios

| ID    | Quality          | Scenario                                                                  | Expected Response                                                                                       |
|-------|------------------|---------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------|
| QS-01 | Privacy          | A database administrator queries the database directly after a sync cycle | Only time ranges (`BusySlot`) are visible; no titles, attendees, or descriptions exist                  |
| QS-02 | Privacy          | A peer instance requests busy slots for a user                            | The response contains only `start` and `end`; all other fields are absent                               |
| QS-03 | Security         | A request arrives at the push endpoint with an unknown peer ID            | The request is rejected with `401 Unauthorized`; no data is stored                                      |
| QS-04 | Security         | The database is breached and OAuth refresh tokens are extracted           | The extracted values are encrypted ciphertext; they cannot be used without the server's encryption key  |
| QS-05 | Resilience       | A peer instance is offline during a scheduled sync cycle                  | The failure is logged; sync continues for all other peers; the failed peer is retried on the next cycle |
| QS-06 | Resilience       | A user's calendar source is temporarily unreachable                       | That user's sync is skipped and retried next cycle; other users are unaffected                          |
| QS-07 | Maintainability  | A developer adds a Google Workspace calendar adapter                      | A new DLL implementing `ICalendarSource` and annotated with `[CalendarSourcePlugin("google", "Google Workspace")]` is placed in `plugins/`; no existing code changes required |
| QS-08 | Self-hostability | A client IT department deploys a new instance                             | Running `docker compose up` with a populated config file produces a working instance                    |
