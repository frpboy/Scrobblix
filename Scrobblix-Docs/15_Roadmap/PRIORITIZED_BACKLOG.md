# Prioritized Backlog

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (\`frpboy\`)  
> **Status:** Active implementation; release gates remain open  
> **Last updated:** 2026-10-21

## Implemented and CI-verified baseline

- GSMTC polling and local playback observation ingestion.
- Canonical playback tracker, eligibility decisions, durable SQLite history, queue leasing and restart recovery.
- Last.fm signature/authentication/submission code and Credential Locker boundary.
- Home, History (recent session page), Queue summary, Sources, Diagnostics and Settings.
- Chromium Browser Companion and Native Messaging registration helpers.
- Source arbitration, local metadata/artwork enrichment, SVG fallback and sequential UTC logs.
- Desktop publish and test-bundle assembly in GitHub Actions.
- This continuation adds search/export and local Statistics; leave them unverified until its CI run passes.

## Remaining P0 — Correctness, security and recovery

- Exponential bounded retry/backoff, Retry-After handling and circuit-breaker policy.
- Reconcile ambiguous Last.fm POST outcomes; never blindly resend a possibly accepted scrobble.
- Harden and test the native-host-to-desktop peer identity boundary.
- Backup/restore, migration rollback and crash-recovery qualification.
- Queue item details/actions with a clear retry/cancel policy.
- Retention controls and sanitized support bundles.

## Remaining P1 — Required v1 experience

- Loved-track list and durable love/unlove commands.
- Firefox extension, manifest/installer registration and lifecycle validation.
- Tray/minimize-to-tray, notifications and activation handling.
- Full history pagination, play details, advanced filters and larger-range export.
- Onboarding, application-specific source controls and accessibility qualification.
- Signed installer/update/repair/uninstall and clean-machine tests.

## Remaining P2 — Strong v1 / beta polish

- Statistics timeline, date ranges, listening reports and provider reliability.
- Backup/restore UX and configuration export with secrets excluded.
- Performance/soak checks, compatibility matrix, About/licenses/attribution and release docs.

## Out of scope for v1

- User metadata rule editor, plugins, third-party providers, Discord, cloud sync and non-Windows clients.
