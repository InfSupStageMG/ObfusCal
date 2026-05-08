# 1. Introduction & Goals

## Purpose

ObfusCal is a self-hosted, open-source web application that eliminates scheduling conflicts for professionals working
across multiple calendar environments and organisational domain boundaries, without exposing sensitive calendar data
outside its originating organisation.

## Background

Software consultants frequently hold active accounts in two or more separate calendar environments simultaneously. One
belonging to their employer and one or more belonging to client organisations. These environments are isolated by
design, a consequence of standard enterprise identity and data governance policies. When a meeting invitation arrives in
one calendar, there is no automated way to verify whether that time is already claimed elsewhere. The conflict only
becomes apparent when it is too late to avoid it.

## Goals

| Priority | Goal                                                                                                    |
|----------|---------------------------------------------------------------------------------------------------------|
| Must     | Exchange availability information across domain boundaries without exposing raw event data              |
| Must     | Allow each organisation to host a sovereign instance with no dependency on a third-party central server |
| Must     | Provide configurable obfuscation rules so each user controls what others see                            |
| Should   | Integrate with Microsoft 365 / Exchange Online as the primary calendar source                           |
| Should   | Remain self-hostable by a client IT department via a single `docker compose up`                         |
| Could    | Expose a booking link so external parties can propose appointments without seeing calendar content      |

These goals directly drive the federated server architecture, obfuscation pipeline, and the strict separation between
raw events and persisted data (see chapters 4 and 5).

## Stakeholders

| Role                                          | Concern                                                                                 |
|-----------------------------------------------|-----------------------------------------------------------------------------------------|
| Consultant                                    | Correct availability is visible to colleagues without leaking sensitive meeting details |
| System Administrator                          | System is secure, auditable, and maintainable within company network policies           |
| Client IT Department                          | Instance can be deployed and operated without understanding application internals       |
| internship company (Coach / Assignment Owner) | Solution is open-source, architecturally sound, and demonstrates privacy-by-design      |
