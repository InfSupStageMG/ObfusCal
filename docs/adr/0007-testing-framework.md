# ADR 0007: Testing framework: MSTest and Testcontainers

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings, Coach (internship company)
* **Date:** 2026-04-20

## Context and problem statement

We needed a test framework that aligns with internship company's internal standards and supports integration testing against
real infrastructure (database) without requiring a persistent external service.

## Considered options

* xUnit + in-memory fakes
* MSTest + Testcontainers
* NUnit + Testcontainers

## Decision outcome

We use **MSTest** as the test framework and **Testcontainers** for integration tests that require a real database.

## Decision rationale

MSTest aligns with Microsoft and internship company standards. Testcontainers allows integration tests to spin up
ephemeral PostgreSQL containers automatically, giving confidence that database logic works correctly without relying on
in-memory approximations that may not reflect real behaviour.

## Consequences

* **Positive:** Aligned with internship company tooling standards.
* **Positive:** Integration tests run against real infrastructure, catching issues that in-memory fakes miss.
* **Negative:** Testcontainers requires Docker to be running on the developer's machine and in CI.
* **Negative:** Container startup adds latency to the integration test suite compared to in-memory tests.
