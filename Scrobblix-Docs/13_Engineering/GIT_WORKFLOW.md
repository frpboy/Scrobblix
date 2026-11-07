# Git Workflow

> **Project:** Scrobblix
> **Owner:** Rahul Muraleedharan (`frpboy`)
> **Status:** Current enforced workflow
> **Last reviewed:** 2026-11-07

## Branches

`main` is the release-intended default branch. Use short-lived `feature/`, `fix/`, `docs/`, `chore/`, and `release/` branches. No long-lived develop branch is used initially.

**Current enforcement readback (2026-11-07):** main protected=true. PR, up-to-date GitHub Actions build-and-test, resolved conversations and linear history required; administrators included; force-push/deletion disabled. Independent approval count zero avoids sole-owner self-approval, not source review. See [Governance](../../GOVERNANCE.md).

## Commits

Conventional Commits with focused changes. Sign commits/tags when practical. Do not commit generated secrets/build outputs/user data.

## Pull requests

Keep changes small enough to review, link issues/requirements, include tests/docs, and add screenshots only for relevant UI work. Do not force-push after review without notification. Use the repository's selected squash or merge policy consistently.

Use pull requests. The maintainer must still check the exact latest head SHA, required CI results, review feedback, unresolved conversations, and dependencies before merging.

## Protection

Current enforced controls are listed above. Signed release tags and a future expanded CODEOWNERS/review quorum are separate policy goals, not currently enforced branch settings. Security-sensitive changes still need source review.
