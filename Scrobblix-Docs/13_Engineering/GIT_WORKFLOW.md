# Git Workflow

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Current workflow with GitHub enforcement caveat  
> **Last reviewed:** 2026-10-31

## Branches

`main` is the release-intended default branch. Use short-lived `feature/`, `fix/`, `docs/`, `chore/`, and `release/` branches. No long-lived develop branch is used initially.

**Current enforcement observation:** On 2026-10-31, the GitHub branch API reported `main` as `protected: false` with `protection.enabled: false`; the repository Rulesets API returned an empty list. The connected integration could not read the dedicated branch-protection endpoint. See [Governance](../../GOVERNANCE.md) and [issue #88](https://github.com/frpboy/Scrobblix/issues/88). Do not assume that required reviews or status checks are currently enforced by GitHub. The maintainer must verify the effective owner settings and configure protection if intended.

## Commits

Conventional Commits with focused changes. Sign commits/tags when practical. Do not commit generated secrets/build outputs/user data.

## Pull requests

Keep changes small enough to review, link issues/requirements, include tests/docs, and add screenshots only for relevant UI work. Do not force-push after review without notification. Use the repository's selected squash or merge policy consistently.

Use pull requests even while branch protection is not enabled. Until platform enforcement is verified, the maintainer must manually check the exact latest head SHA, required CI results, review feedback, unresolved conversations, and dependencies before merging.

## Protection

The following are intended governance controls, not claims about the currently enabled GitHub configuration: required reviews/checks, conversation resolution, no direct pushes, signed release tags, and CODEOWNERS when maintainers expand. Security-sensitive workflows require maintainer approval. Record the effective GitHub settings in this document after an authorized owner read-back.
