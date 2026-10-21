# Screen Specifications

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


Every screen must define purpose, entry points, data dependencies, states, actions, accessibility, and diagnostics.

## Screen inventory

| ID | Screen | Primary purpose |
|---|---|---|
| UI-001 | Onboarding | Establish consent, account, sources, and startup behavior |
| UI-010 | Home | Current playback and overall health |
| UI-020 | History/Timeline | Audit and manage local listening records |
| UI-030 | Play Detail | Explain one canonical play end-to-end |
| UI-040 | Statistics | Explore local listening patterns and reliability |
| UI-050 | Loved | Browse and manage loved-track state |
| UI-060 | Queue | Manage remote actions and failures |
| UI-070 | Queue Detail | Inspect payload, attempts, response, and next action |
| UI-080 | Diagnostics Overview | Health and recommended repairs |
| UI-081 | Live Inspector | Raw observations, canonicalization, and state transitions |
| UI-090 | Sources & Browsers | Manage applications, sites, extension, and adapters |
| UI-100 | Settings | Configure behavior, privacy, storage, and updates |
| UI-110 | About | Version, licenses, API attribution, and support links |
| UI-120 | Tray Menu | Quick status and commands |
| UI-121 | Mini Player/Flyout | Current track, eligibility, and quick actions |
| UI-130 | Export | Select range/format/fields and write export |
| UI-140 | Backup/Restore | Protect and recover local data |
| UI-150 | Support Bundle | Preview and create sanitized diagnostics |

## Common states required on every data screen

- Initial loading/skeleton.
- Loaded with data.
- Empty/first-use.
- Filtered empty.
- Partial/degraded data.
- Offline where relevant.
- Permission/authentication required.
- Recoverable error.
- Non-recoverable error with support path.

## Common command behavior

- Commands display disabled rationale via tooltip/help text when unavailable.
- Long operations expose cancel where safe.
- Success confirmation is non-modal unless the result materially changes data.
- Destructive commands state exact scope and whether provider data is affected.
- Retry commands are idempotent from the user's perspective.
