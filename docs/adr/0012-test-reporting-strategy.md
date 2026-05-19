# ADR 0012: CI/CD Test Reporting Strategy

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings
* **Date:** 2026-05-19

## Context and problem statement

As the project matures, we need to make our automated test results insightful and persistently available. Raw `.trx` (
XML) files are difficult for humans to read. While there are many third-party GitHub Actions and NuGet packages (e.g.,
`trx2html`, `LiquidTestReports`) available to format these reports, we must evaluate if the convenience of these tools
outweighs the security and reliability risks they introduce.

## Considered options

1. **Third-party GitHub Actions** (e.g., `dorny/test-reporter`)
2. **Third-party NuGet Loggers** (e.g., `LiquidTestReports`)
3. **Native Microsoft HTML Logger + Python XML Parsing**

## Decision outcome

We chose **Option 3: Native Microsoft HTML Logger + Python XML Parsing**.

We don't want to use third-party test reporter actions and single-maintainer NuGet packages. Instead, we will generate a
standalone HTML file using Microsoft's native `.NET VSTest` framework (`dotnet test --logger html`) to serve as a
downloadable artifact. Concurrently, we will use a native Python script (pre-installed on CI runners) to parse the
`.trx` XML and write a rich Markdown summary directly to the GitHub UI.

## Decision rationale

1. **Security (Supply Chain):** Single-maintainer open-source packages carry a high risk of abandonment or supply-chain
   compromise. By relying exclusively on Microsoft's built-in SDK tools and standard Python, our attack surface remains
   near-zero.
2. **Security (Permissions):** Most third-party test reporters require elevating the `GITHUB_TOKEN` to have
   `pull-requests: write` access. Our native approach allows us to keep pipeline permissions strictly locked down to
   `contents: read`.
3. **UX & Reliability:** The native HTML logger provides a clean, structured file for QA to download, while the
   Python script ensures developers get immediate, expandable error logs directly in the GitHub Actions UI without
   downloading anything.

## Consequences

* **Positive:** Zero third-party dependencies are introduced to the CI/CD pipeline.
* **Positive:** Pipeline permissions remain strictly locked down (Principle of Least Privilege).
* **Positive:** Test metrics and specific failure messages are instantly visible in the GitHub UI.
