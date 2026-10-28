# Feature Catalog — Implementation and Qualification Status

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Document type:** Product planning catalog, not a release certification  
> **Last reviewed against source:** 2026-10-28

## Status definitions

- **Implemented, qualify:** corresponding code/UI surfaces exist; acceptance on real Windows/provider/browser still must be proven.
- **Partial:** only part of the requested capability is implemented or a known gap remains.
- **Planned:** not established as a shipped feature in the current source baseline.
- Priority is product importance, not current completion.

| ID | Feature | User value | Priority | Current status |
|---|---|---|---|---|
| F-001 | GSMTC detection | Read Windows media-session metadata for desktop playback | Must | **Partial / qualify.** Current-session observation adapter and polling path exist; broad player compatibility and sleep/wake/restart behavior remain unverified. |
| F-002 | Browser Companion | Supply site/tab-level media metadata over Native Messaging | Must | **Partial / qualify.** Chromium/Firefox development integration exists; live browser installation/events remain unverified, and manifest vs built-in YouTube origin allow-list mismatch is tracked in [issue #73](https://github.com/frpboy/Scrobblix/issues/73). |
| F-003 | Source arbitration | Reconcile overlapping GSMTC/browser observations | Must | **Implemented, qualify.** Domain/application arbitration and canonical play tracking exist; real multi-source duplicate/replay correctness needs manual evidence. |
| F-004 | Eligibility tracker | Show confirmed playing time and scrobble threshold | Must | **Implemented, qualify.** Domain rule is in `Scrobblix.Domain/Eligibility/ScrobbleEligibility.cs`; real-player pause/seek/replay correctness remains unverified. |
| F-005 | Now Playing | Publish current media to Last.fm | Must | **Implemented, qualify.** Provider action exists; live Last.fm acceptance has not been confirmed in this review. |
| F-006 | Durable scrobble queue | Preserve eligible actions during restart/outage | Must | **Implemented, qualify.** SQLite provider actions, idempotency keys, leases and recovery exist; multi-day no-loss/no-duplicate evidence remains open. |
| F-007 | Retry engine | Recover transient provider failures safely | Must | **Partial / qualify.** Bounded retry and Retry-After support exist; full jitter, a circuit breaker, batch submission and long-run provider recovery are not implemented/proven. |
| F-008 | Local history | Retain local observed/canonical play records | Must | **Implemented, qualify.** SQLite observations/plays and history UI/export exist; completeness and retention/deletion workflows need verification. |
| F-009 | Queue page | Inspect and recover local provider actions | Must | **Implemented, qualify.** Queue and safe action-state commands exist; provider state correctness must be tested with disposable data. |
| F-010 | Diagnostics | Explain source, metadata, timing, storage and provider outcomes | Must | **Partial / qualify.** UI/local logs/reason codes/support bundle exist; not every proposed pipeline stage or metric is surfaced. |
| F-011 | Tray app | Operate unobtrusively in the Windows notification area | Must | **Partial / qualify.** Tray icon, open/exit menu and minimize-to-tray code exist; Explorer/taskbar restart and real shell behavior remain unverified. |
| F-012 | Last.fm authorization | Approve account in browser, store session securely | Must | **Implemented, qualify.** Token/session exchange and Credential Locker path exist; live authorization/revocation/account-switch scenarios remain unverified. |
| F-013 | Love/Unlove | Manage loved state through provider actions | Should | **Implemented, qualify.** Durable action/state implementation exists; live provider behavior and same-track sequencing remain unverified. |
| F-014 | Statistics | Summarize local listening data | Should | **Implemented, qualify.** SQLite summary/statistics surfaces exist; advanced visualization breadth in product designs must be checked against the current UI. |
| F-015 | Listening timeline | Review local play history chronologically | Should | **Partial.** History view/query surface exists; do not assume a separate advanced timeline visualization is shipped. |
| F-016 | Search and filters | Find local plays by available fields | Should | **Implemented, qualify.** Search/filter/export surface exists; field coverage and large-history performance remain to be validated. |
| F-017 | Browser/source health | Troubleshoot source/host readiness | Must | **Partial / qualify.** Sources and explicit popup host diagnostics exist; registration is not proof of event delivery and origin mismatch remains open. |
| F-018 | Notifications | Give optional delivery/privacy-aware status notices | Should | **Implemented, qualify.** Notification preference and privacy controls exist, off by default; activation/lock-screen behavior needs Windows testing. |
| F-019 | Export | Export local listening records | Must | **Partial.** CSV export exists; do not advertise JSON/range export beyond actual UI behavior without further implementation evidence. |
| F-020 | Backup/restore | Recover the local database safely | Must | **Implemented, qualify.** ZIP/checksum/schema validation, staged restore and safety snapshot mechanisms exist; corruption/locked-sidecar rollback matrix remains open. |
| F-021 | Privacy controls | Control retention, data deletion, cache and telemetry | Must | **Partial.** Credential, notification/privacy and support-export controls exist; full retention/deletion UX and telemetry control are not established as shipped. |
| F-022 | Support bundle | Share minimized diagnostics with support | Must | **Partial / qualify.** Diagnostics-only format-v2 ZIP exists; seeded-canary and real Windows Save/export/accessibility gates remain open. |
| F-023 | App updater | Discover/install verified app updates | Must | **Planned.** Current releases are unsigned preview ZIPs; no signed in-app updater is established. |
| F-024 | Global shortcut | Trigger common actions quickly | Could | **Planned.** Not established as implemented in current source. |
| F-025 | Metadata rules | Preview user-defined metadata transformations | Later | **Planned.** |
| F-026 | Multiple providers | Add providers beyond Last.fm | Later | **Planned.** |
| F-027 | Plugins | Support third-party adapters | Later | **Planned.** |
| F-028 | Cloud sync | Sync history/settings between devices | Later | **Out of initial scope.** Requires separate privacy/security/product decision. |

## Feature completion rule

A code surface is not a fully released feature. A Must feature is ready only when implementation and tests exist, errors/recovery are documented, applicable automated checks pass on the exact candidate SHA, and manual-only requirements have recorded evidence. Never translate “implemented, qualify” into “fully tested” on a release page.
