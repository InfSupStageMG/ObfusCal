# 6. Runtime View

## Scenario 1: Background Sync Cycle

This is the primary runtime scenario. The `SyncService` runs on a configurable interval (default: 15 minutes).

![Sync Sequence Diagram](img/sequence-sync.png)

**Narrative:**

1. The background scheduler wakes up and iterates over all active peer connections.
2. For each connection, it fetches the user's raw events from the calendar source (MS Graph, iCal, etc.).
3. The raw `CalendarEvent` objects are passed to `ObfuscationPipeline.Process()`, which applies configured
   transformers and returns a list of `BusySlot` objects.
4. The raw events immediately fall out of scope and are garbage collected. They are never written to disk.
5. The obfuscated `BusySlot` list is POSTed to the peer instance's `/api/shadow-slots` endpoint (push).
6. External obfuscated slots are ingested via `POST /api/shadow-slots` (from trusted peer IDs) and saved in
   `IShadowSlotStore`.
7. If a peer is unreachable, the failure is logged and that peer is skipped. Other peers are unaffected.

## Scenario 2: Calendar Owner Requests Busy Slots

An authenticated calendar owner requests their own obfuscated availability window.

1. Client sends `GET /api/calendar-owners/{id}/busy-slots?from=...&to=...` with a valid Entra bearer token.
2. The API resolves the Entra `oid` to a local `CalendarOwner` record.
3. If no owner record is found, the API returns `404 Not Found`.
4. If `{id}` does not match the authenticated owner, the API returns `403 Forbidden`.
5. For authorized requests, the calendar source is queried and obfuscated slots are returned (`start` and `end` only).

## Scenario 3: User Authenticates and Views Availability

1. User opens Swagger UI or another API client.
2. The application redirects to Info Support's Entra ID login page via OpenID Connect.
3. After successful authentication (including MFA), Entra ID returns a JWT token.
4. The application extracts the user's Object ID from the token and scopes all subsequent data access to that ID.
5. The user can call `GET /api/calendar-owners/{id}/merged-freebusy` to view their own merged availability.

## Scenario 4: Asymmetric Sync (No Peer Instance)

This scenario occurs when a client organization does not run an ObfusCal peer instance.

1. The consultant provides a read-only .ics sharing link from their client-side calendar (e.g., Outlook Web).
2. During the background sync cycle, the ICalFeedCalendarSource fetches and parses this feed into raw CalendarEvent objects.
3. The events are passed through the obfuscation pipeline, producing obfuscated BusySlot data.
4. The resulting slots are stored locally as ShadowSlots in the IShadowSlotStore.
5. Since no peer instance is available, the system does not attempt to push BusySlots via a REST API.
6. Client contacts access availability through the consultant’s booking link or a generated .ics subscription.
7. When accessed, the system queries local data, merges internal commitments with stored shadow slots, and renders a unified free/busy view.
