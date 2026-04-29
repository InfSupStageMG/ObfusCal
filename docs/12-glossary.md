# 12. Glossary

| Term                        | Definition                                                                                                                                                                        |
|-----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BusySlot**                | The obfuscated output of the pipeline. It always contains start/end and may include optional metadata fields (title/description/attendees/location) when allowed by profile.      |
| **CalendarEvent**           | An in-memory object representing a raw event fetched from a calendar source. It is never stored and is discarded immediately after obfuscation.                                   |
| **Calendar Owner**          | The authenticated user identity in ObfusCal. Data access is scoped to the owner mapped from the Entra ID object ID (`oid`).                                                       |
| **Free/Busy View**          | The combined view of a calendar owner's own obfuscated busy slots and shadow slots received from peers. It shows availability without exposing meeting details.                   |
| **Horizontal Merge**        | The process of combining overlapping or simultaneous calendar blocks into a single busy block.                                                                                    |
| **iCal Feed / ICS**         | A standard read-only calendar feed used as a fallback `ICalendarSource` for client domains unable to host a peer instance.                                                        |
| **ICalendarSource**         | The core interface for all calendar input mechanisms (e.g., Graph API, iCal). It decouples the obfuscation pipeline from specific calendar providers.                             |
| **IObfuscationTransformer** | A single, chainable step in the obfuscation pipeline (e.g., remove title, round times) that transforms a `CalendarEvent`.                                                         |
| **IBusySlotTransformer**    | A post-processing transformer for busy slots (e.g., merge overlapping or adjacent blocks).                                                                                        |
| **Instance**                | A single server deployment of the ObfusCal application (e.g., the Info Support server or a client server).                                                                        |
| **IShadowSlotStore**        | The storage layer for shadow slots received from peer instances. Data is isolated by peer ID and populated only via inbound sync calls.                                           |
| **Merge**                   | Combining multiple calendar blocks into fewer, larger blocks to hide meeting patterns and prevent duration fingerprinting.                                                        |
| **ObfuscationPipeline**     | The in-memory service that acts as a privacy gatekeeper, passing raw `CalendarEvent`s through transformer steps to produce safe `BusySlot`s.                                      |
| **ObfuscationAuditContext** | Context marker used for audit logging (`Client` vs `Internal`) when running the pipeline.                                                                                         |
| **Peer Connection**         | A record representing a trusted relationship with a remote instance, containing the target URL and sync metadata.                                                                 |
| **Peer Instance**           | An independent ObfusCal server hosted in an external client's domain, communicating via the REST API.                                                                             |
| **Shadow Calendar**         | An aggregated view of all external shadow slots belonging to a single calendar owner.                                                                                             |
| **Shadow Slot**             | A `BusySlot` originating from an external peer instance. It is stored locally and merged into the calendar owner's free/busy view.                                                |
| **Sync Loop**               | The periodic process that fetches calendar data, runs obfuscation, and exchanges slots with peers.                                                                                |
| **Vertical Merge**          | The process of combining adjacent or consecutive calendar blocks into a single continuous busy block.                                                                             |
| **View**                    | The user-facing overview of a calendar owner's merged availability across all internal and external sources.                                                                      |
