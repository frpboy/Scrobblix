# Delivery Plan

## Phase 0 — Repository and decision baseline

- Establish monorepo, licences, contribution/security policy, CI, coding standards, dependency policy, and ADR workflow.
- Convert open architecture assumptions into accepted ADRs.
- Define supported Windows/browser matrix.
- Obtain a compliant Last.fm API application and document key handling.

## Phase 1 — Deterministic core

- Domain models and state machines.
- Synthetic observation fixtures.
- Played-time and eligibility engine.
- Canonical source arbitration and duplicate prevention.
- SQLite schema, migrations, queue leasing, and restart recovery.
- Simulated provider adapter and complete reason codes.

Exit criterion: deterministic test suite proves that one logical play yields no more than one logical scrobble action across pause, seek, replay, restart, overlap, and retry cases.

## Phase 2 — Windows vertical slice

- GSMTC adapter.
- Credential storage and Last.fm authentication.
- Provider now-playing/scrobble worker.
- Tray host, Home, Queue, and Diagnostics.
- Signed internal package.

Exit criterion: representative desktop and browser sessions scrobble reliably with auditable local state.

## Phase 3 — Browser precision

- TypeScript extension core.
- Chromium MV3 implementation.
- Firefox implementation.
- Native Messaging host and secure internal IPC.
- Generic media detector and prioritized site adapters.
- Permission and privacy UX.

Exit criterion: supported browser matrix passes metadata, pause, seek, tab switch, sleep/resume, update, and version-mismatch cases.

## Phase 4 — Product completeness

- History/timeline, statistics, loved tracks.
- Export, backup, restore, retention, privacy controls.
- Notifications, accessibility, localization readiness.
- Installer/update rollback and support bundle.

## Phase 5 — Public v1

- Threat-model review and security tests.
- Performance and soak tests.
- Name/IP and dependency-licence review.
- User documentation and screenshots.
- Beta feedback closure.
- Signed release, checksums, SBOM, release notes, and incident ownership.
