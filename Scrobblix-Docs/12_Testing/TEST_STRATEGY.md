# Test Strategy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Quality model

Scrobblix prioritizes data durability, deterministic timing, duplicate prevention, source compatibility, security/privacy, and diagnostic explainability. Test pyramid: many domain unit/property tests, focused infrastructure integration tests, and smaller end-to-end/UI/browser suites.

## Test environments

- Pure .NET domain tests on CI.
- SQLite temporary databases and migration corpus.
- Mock Last.fm server plus limited real test account smoke tests.
- Windows 11 VM/matrix for GSMTC/WinUI/packaging.
- Chromium/Firefox automated extension tests.
- Signed release candidate install/update/uninstall lab.

## Fault injection

Network loss, timeout after send, rate limits, invalid session, malformed responses, process termination at persistence boundaries, disk full, database lock/corruption, sleep/resume, clock jump, extension disconnect/version mismatch, event duplication/reordering.

## Release evidence

CI test reports, coverage (used as signal not goal), benchmark comparison, compatibility results, accessibility report, vulnerability/dependency scan, migration report, package signature verification, and manual exploratory checklist.
