---
name: User Story / Feature
about: Refinement-ready issue template for calendar sync PoC
labels: refinement-needed
---

## Description

<!-- Provide a clear, concise description of the feature or change.
     Use the format: As a [role], I want [goal], so that [reason]. -->

**User story**
As a _[consultant / client / system]_,
I want _[capability or action]_,
so that _[business value or outcome]_.

**Context & background**
<!-- Why does this issue exist? Link to relevant architecture docs,
     ADRs, related issues or discussions. -->

**Out of scope**
<!-- Explicitly list what this issue does NOT cover, to avoid scope creep. -->

---

## Definition of Ready
<!-- All boxes must be checked before this issue enters a sprint. -->

- [ ] User story is clearly written and understood by the team
- [ ] Acceptance criteria are defined and unambiguous
- [ ] Dependencies on other issues or external systems are identified
- [ ] Security and privacy implications have been considered
- [ ] Issue is estimated (story points or t-shirt size)
- [ ] Design or architectural decisions needed are documented or linked
- [ ] No unresolved blocking questions remain

---

## Acceptance Criteria
<!-- List of deliverables to fulfill this user story, written as short bullet points. -->

- On page xyz there is a submit button which ...
- The solution contains ...
- ...

<!-- Add more deliverables as needed. -->

---

## Security & Privacy Checklist
<!-- Mandatory for all issues touching calendar data, network, or file I/O.
     Keep only which item(s) fit. -->

- [ ] No sensitive calendar fields (title, attendees, location) leave the local domain unobfuscated
- [ ] Data at rest is not stored outside the user's own domain
- [ ] Any transmitted payload is encrypted end-to-end
- [ ] No credentials, tokens or keys are logged or hardcoded
- [ ] N/A — this issue does not touch sensitive data or network I/O

---

## Dependencies & Related

| Type | Reference |
|------|-----------|
| Blocks | #issue / none |
| Blocked by | #issue / none |
| Related | #issue / ADR / doc link |
| External | library, API, service name |

---

## Definition of Done
<!-- All boxes must be checked before the issue can be closed. -->

- [ ] All acceptance criteria have been verified manually
- [ ] dotnet build produces zero errors and zero warnings
- [ ] All tests pass with dotnet test and no tests are skipped
- [ ] New code has corresponding unit or integration tests covering the happy path and key failure cases
- [ ] No sensitive data (calendar fields, credentials, tokens) appears in logs, responses, or committed files
- [ ] New configuration values are documented in .env.example or appsettings.Development.json where applicable
- [ ] Code has been self-reviewed before opening a PR (no debug leftovers, no commented-out code, no TODOs without a linked issue)
- [ ] Every PR is reviewed and approved by at least one other team member before merging
- [ ] All PRs for this issue are merged and the CI pipeline passes on main

---

## Metadata

| Field | Value |
|-------|-------|
| Priority | Critical / High / Medium / Low |

---

## Notes & Open Questions
<!-- Parking lot for things that came up during refinement but are not
     yet resolved. Each question should be followed by a decision or
     a link to where it will be resolved. -->

- Notes: ...
- Open question: ...
