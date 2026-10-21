# AI Agent Guide

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Authority order

1. Current user/maintainer instruction.
2. Security/privacy/product rules.
3. Requirements/ADRs/schema/contracts.
4. Existing tests and implementation.
5. General coding preferences.

Do not silently override docs with assumptions. Flag conflicts and update the authoritative document in the same change when a decision changes.

## Required behavior

- Read relevant docs before code.
- Preserve existing functionality unless change explicitly redesigns it.
- Produce complete production-quality implementation, not stubs/placeholders.
- Respect Clean Architecture dependency direction.
- Add tests for behavior and regression.
- Implement errors/retries/cancellation/recovery/diagnostics.
- Never expose secrets or use real user data.
- Do not introduce cloud backend, audio capture, broad browser permissions, or provider changes without approved decision.
- Update requirements, ADRs, schema, reason codes, UX, and changelog as applicable.

## Forbidden shortcuts

- Mock repository in production path.
- In-memory queue as substitute for SQLite durability.
- Wall-clock-only played time.
- Swallowing exceptions.
- Generic localhost server without security design.
- Extension directly calling Last.fm.
- Guessing metadata from filenames/window titles generically.
- Marking provider action successful before acknowledgement.
- Disabling tests/analyzers to merge.

## Change output

Summarize files changed, requirement IDs, architecture impact, migrations, tests, manual verification, known limitations, and follow-up issues. Be explicit when environment prevents verification.
