# ADR 0012: CI/CD Test Reporting Strategy

* **Status:** Accepted
* **Deciders:** Matthias Hendrickx, Gijs Pennings
* **Date:** 2026-05-19

## Context and problem statement

As the project matures, we need to make our automated test results persistently available as artifacts and immediately
visible in the CI/CD pipeline output. While there are many third-party GitHub Actions available in the marketplace to
parse and publish test results (e.g., `dorny/test-reporter` or `EnricoMi/publish-unit-test-result-action`), we need to
decide if the convenience of these tools outweighs the security and reliability risks they introduce to our supply
chain.

## Considered options

* Third-party GitHub Actions from the marketplace
* Native .NET tooling combined with GitHub's built-in Step Summaries and Artifacts

## Decision outcome

We chose **Native .NET tooling combined with GitHub's built-in Step Summaries and Artifacts**.

We explicitly reject the use of third-party test reporter actions. Instead, we will output `.trx` files using
`dotnet test --logger trx`, parse the summary via standard Linux CLI utilities (`grep`), and write it directly to the
`$GITHUB_STEP_SUMMARY` environment variable.

## Decision rationale

1. **Security (Supply Chain):** Third-party actions require continuous trust. Compromised actions are a common
   supply-chain attack vector. By avoiding them, we reduce our attack surface.
2. **Security (Permissions):** Most third-party test reporters require elevating the `GITHUB_TOKEN` to have
   `pull-requests: write` access so they can post comments. Our native approach requires only `contents: read`, strictly
   adhering to the Principle of Least Privilege.
3. **Reliability:** Standard POSIX utilities and native GitHub features will not break due to underlying runner
   changes (e.g., Node.js version deprecations), which frequently plague unmaintained marketplace actions.

## Consequences

* **Positive:** Zero third-party dependencies are introduced to the CI/CD pipeline.
* **Positive:** Pipeline permissions remain strictly locked down.
* **Positive:** Test metrics are instantly visible on the GitHub Actions summary page without cluttering the Pull
  Request timeline with bot comments.
* **Negative:** The visual presentation in the Step Summary is basic text/markdown, lacking the rich graphical UI that
  some dedicated third-party reporters provide.
