# 2. Architecture Constraints

## Technical Constraints

| Constraint                                                     | Reason                                                                                                                                                                               |
|----------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| No globally shared server or storage                           | Core project requirement. A central server would require all participating organisations to trust a third party with availability metadata, which regulated clients will not accept. |
| Self-hostable via Docker                                       | Client IT departments must be able to run an instance without understanding the application internals. `docker compose up` is the target deployment experience.                      |
| .NET 10 (C#)                                                   | Primary language chosen for team expertise and internship company's in-house knowledge.                                                                                              |
| Microsoft 365 / Exchange Online as the primary calendar source | The production target specified by the assignment. OAuth 2.0 delegated permissions via the Graph API is required.                                                                    |
| All cross-domain data exchange over HTTPS                      | Raw event data must never travel across a domain boundary. Only obfuscated busy slots may be transmitted.                                                                            |
| Open-source licence                                            | The solution itself must be open-source (GPL v3). All runtime dependencies must have compatible licences.                                                                            |

## Organisational Constraints

| Constraint                               | Reason                                                                                                      |
|------------------------------------------|-------------------------------------------------------------------------------------------------------------|
| Internship project scope                 | Full production hardening (e.g. multi-region HA, advanced audit logging) is out of scope for the PoC phase. |
| Internship company development standards | ADRs follow the internship company guidance framework. Testing uses MSTest per company standard.            |
| No proprietary forks                     | Vendor SDKs (Graph, MSAL) are acceptable as runtime dependencies but may not be forked or modified.         |

## Conventions

| Convention        | Value                                                             |
|-------------------|-------------------------------------------------------------------|
| Language          | C# / .NET 10                                                      |
| API style         | REST with OpenAPI / Swagger                                       |
| Container runtime | Docker (Compose for local dev)                                    |
| Source control    | GitHub                                                            |
| Documentation     | arc42 in Markdown, published via MkDocs                           |
| Decision records  | ADR format per internship company guidance, stored in `docs/adr/` |
