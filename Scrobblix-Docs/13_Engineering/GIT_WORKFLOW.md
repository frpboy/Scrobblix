# Git Workflow

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Branches

Protected `main` remains releasable. Short-lived `feature/`, `fix/`, `docs/`, `chore/`, `release/` branches. No long-lived develop branch initially.

## Commits

Conventional Commits with focused changes. Sign commits/tags when practical. Do not commit generated secrets/build outputs/user data.

## Pull requests

Small enough to review, linked issue/requirements, tests/docs included, screenshots only for relevant UI, no force-push after review without notification. Squash or merge policy selected consistently.

## Protection

Required reviews/checks, conversation resolution, no direct pushes, signed release tags, CODEOWNERS when maintainers expand. Security-sensitive workflows require maintainer approval.
