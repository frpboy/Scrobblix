# Contributing to Scrobblix

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


Scrobblix accepts bug reports, documentation improvements, test cases, source adapters, browser-site adapters, accessibility fixes, translations, and implementation changes that follow the documented architecture.

## Before contributing

1. Read [`00_Project/PRODUCT_RULES.md`](00_Project/PRODUCT_RULES.md).
2. Read [`05_Architecture/ARCHITECTURE.md`](05_Architecture/ARCHITECTURE.md) and applicable ADRs.
3. Search existing issues and discussions.
4. For architectural changes, open a proposal before implementation.
5. Never include real Last.fm session keys, API secrets, personal listening exports, or copyrighted album art in tests.

## Change categories

| Category | Required evidence |
|---|---|
| Bug fix | Reproduction, failing test, fix, regression test |
| Feature | Requirement ID, design notes, tests, documentation update |
| Source/site adapter | Sample metadata fixtures, compatibility matrix update, privacy review |
| Database change | Migration, rollback/forward recovery plan, schema documentation update |
| UI change | Screen specification update, keyboard/accessibility verification, screenshots where useful |
| Security change | Threat-model delta and security review |

## Pull request standard

A pull request must state:

- the user-visible problem;
- linked issue and requirement IDs;
- architecture impact;
- security/privacy impact;
- database or migration impact;
- tests added or changed;
- documentation changed;
- manual verification performed;
- rollback considerations.

Incomplete code, placeholder exception handling, disabled tests, silent catches, and untracked TODO comments are not accepted in production paths.

## Commit format

Use Conventional Commits:

```text
feat(engine): add deterministic source arbitration
fix(lastfm): preserve cached scrobble timestamp during retry
docs(ux): define queue failure detail panel
test(gsmtc): cover paused-to-stopped transition
```

## Development branches

- `main`: protected and releasable.
- `feature/<issue>-<slug>`: ordinary work.
- `fix/<issue>-<slug>`: bug fixes.
- `release/<version>`: only when release stabilization requires it.

## Developer certificate

By contributing, you certify that you have the right to submit the work under the repository license. A Developer Certificate of Origin sign-off may be enabled later if contributor volume warrants it.
