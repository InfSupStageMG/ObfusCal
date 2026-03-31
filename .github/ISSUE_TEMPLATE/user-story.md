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
<!-- Written as testable Given / When / Then statements.
     Each criterion must be independently verifiable. -->

**Scenario 1:** _[short scenario name]_
```
Given  [a precondition or context]
When   [an action is performed]
Then   [an observable, testable outcome]
```

**Scenario 2:** _[short scenario name]_
```
Given  [a precondition or context]
When   [an action is performed]
Then   [an observable, testable outcome]
And    [optional additional outcome]
```

<!-- Add more scenarios as needed. -->

---

## Security & Privacy Checklist
<!-- Mandatory for all issues touching calendar data, network, or file I/O. -->

- [ ] No sensitive calendar fields (title, attendees, location) leave the local domain unobfuscated
- [ ] Data at rest is not stored outside the user's own domain
- [ ] Any transmitted payload is encrypted end-to-end
- [ ] No credentials, tokens or keys are logged or hardcoded
- [ ] N/A — this issue does not touch sensitive data or network I/O

---

## Definition of Done
<!-- All boxes must be checked before the issue can be closed. -->

- [ ] All acceptance criteria pass
- [ ] Unit tests written and passing (coverage ≥ 80% for new code)
- [ ] Integration or end-to-end test added where applicable
- [ ] Security & privacy checklist completed
- [ ] Code reviewed and approved by at least one other team member
- [ ] No new compiler warnings or unhandled exceptions introduced
- [ ] README / docs updated if public behaviour or setup changed
- [ ] Issue linked to a PR and PR is merged to the target branch

---

## Dependencies & Related

| Type | Reference |
|------|-----------|
| Blocks | #issue / none |
| Blocked by | #issue / none |
| Related | #issue / ADR / doc link |
| External | library, API, service name |

---

## Metadata

| Field | Value |
|-------|-------|
| Component | Core sync / Obfuscation / Transport / UI / Infra |
| Estimate | S / M / L / XL |
| Priority | Critical / High / Medium / Low |
| Sprint | Backlog / Sprint N |
| Assigned to | @handle |

---

## Notes & Open Questions
<!-- Parking lot for things that came up during refinement but are not
     yet resolved. Each question should be followed by a decision or
     a link to where it will be resolved. -->

- [ ] Open question: ...
- [ ] Open question: ...