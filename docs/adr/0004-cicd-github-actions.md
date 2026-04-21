# ADR 0004: CI/CD Pipeline: GitHub Actions and GHCR

**Status:** Accepted  
**Deciders:** Matthias Hendrickx, Gijs Pennings  
**Date:** 2026-04-20

## Context and Problem Statement

The project is hosted on GitHub and needs automated builds so that the latest Docker image is always available after a
merge without manual intervention.

## Considered Options

- GitHub Actions + GitHub Container Registry (GHCR)
- GitHub Actions + Docker Hub
- Azure DevOps Pipelines + Azure Container Registry

## Decision Outcome

We use **GitHub Actions** for the pipeline and **GHCR** as the image registry.

## Decision Rationale

GHCR is co-located with the source repository, requires no external account, and authenticates using the built-in
`GITHUB_TOKEN`. Thus giving us no credentials to manage. GitHub Actions integrates directly with GHCR and is already
familiar to the team.

## Consequences

- **Positive:** No external registry account or credential management needed.
- **Positive:** Images, source code, and issues all live in one place.
- **Negative:** Ties the project to GitHub's infrastructure; migrating later would require moving both the repo and the
  registry.
