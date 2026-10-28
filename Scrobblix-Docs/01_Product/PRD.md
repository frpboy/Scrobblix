# Product Requirements Document

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Document type:** Product target and normative requirement baseline (not an implementation inventory)  
> **Last reviewed against source:** 2026-10-28

## Status convention — read before using this PRD

This document describes desired product behavior. A requirement being written here is **not evidence that it is implemented or qualified**. Current code inventory belongs in the root [README](../../README.md), the implementation-backed [System Architecture](../05_Architecture/ARCHITECTURE.md), the [Release Checklist](../12_Testing/RELEASE_CHECKLIST.md), and the [Real-world Manual Test Matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md).

As of this review:
- Native WinUI app surfaces, GSMTC current-session observation, local SQLite history/queue, Last.fm authorization and action paths, backup/restore code, opt-in notification handling, and the development Browser Companion exist in source.
- Browser built-in-origin consistency is incomplete: generic YouTube manifest matches do not align with the narrower code-level origin allow-list; see [issue #73](https://github.com/frpboy/Scrobblix/issues/73).
- Signed install/update, store distribution, full retention/deletion workflows, comprehensive external telemetry, and a production updater are not established as shipped capabilities.
- Real-Windows, live Last.fm, installed-browser, accessibility, and endurance acceptance remains an independent gate; do not infer it from source presence or CI.

## 1. Product overview

Scrobblix is a privacy-first Windows listening companion that observes media playback, tracks qualifying listening time, submits eligible plays to Last.fm, and maintains local listening history and provider-action state. It has a WinUI desktop interface and notification-area behavior, with an optional browser companion for additional source metadata.

## 2. Problem

A missing Last.fm scrobble may originate at multiple layers: no source session, incomplete metadata, invalid duration, incorrect pause/seek accounting, source switching, duplicate suppression, sleep/resume, local persistence, network failure, provider rejection, ambiguous POST outcomes or expired authentication. The product should show the actual layer/reason and provide a safe recovery path rather than collapsing failures into “nothing happened.”

## 3. Goals

- Detect supported desktop/browser playback with transparent source identity and state.
- Apply Last.fm scrobble eligibility using confirmed elapsed playing time.
- Persist local history and provider actions before relying on remote delivery.
- Suppress duplicate observations without confusing a genuine repeated play with the prior play.
- Preserve queued work across restart and transient provider/network failures.
- Explain accepted, ignored, retryable, authentication-blocked, permanent and ambiguous outcomes.
- Keep local history usable without Last.fm authorization.
- Protect credentials and private listening data through local-first design and explicit export controls.

## 4. Non-goals

Scrobblix is not an audio-recognition service, music player, streaming platform, cloud account service or DRM bypass. Audio capture, acoustic fingerprinting, remote listening analytics and cloud history sync are outside the initial release scope.

See [Project Scope](../00_Project/SCOPE.md) and approved architecture decisions for scope authority.

## 5. Primary users

- Last.fm listener who wants dependable desktop scrobbling.
- Multi-player user who moves between supported media sources.
- Privacy-conscious listener who wants local history and transparent remote submission.
- Developer/tester diagnosing compatibility and reliability issues.

## 6. Product capabilities and current implementation boundary

### 6.1 Onboarding and Last.fm setup

**Target:** Explain local processing and provide a local-history-only path, save API credentials securely, launch Last.fm browser authorization, and show the connected account/clear status.

**Current:** Settings supports Last.fm API key/shared-secret entry, Credential Locker storage, browser authorization token/session exchange, connection state and disconnect. The connection flow has not been qualified against a live Last.fm account as part of this documentation work. Do not claim a complete first-run onboarding wizard or automatic launch-at-sign-in unless separately verified in the current build.

### 6.2 Current playback and tracking

**Target:** Show current media, source, playback state, confirmed playing time, duration, eligibility threshold and delivery status.

**Current:** Home displays current playback/delivery surfaces. The GSMTC adapter obtains the current Windows media session and the app polls/ingests observations. Confirmed played time is tracked separately from current media position. Missing media metadata remains a real-world compatibility concern.

### 6.3 Detection

**Current:** The Windows GSMTC current-session path and an optional Chromium/Firefox development companion exist. The browser extension has built-in observers for specific sites and supports explicit user-registered custom origins. Built-in sender validation currently accepts `https://music.youtube.com` and `https://open.spotify.com`; generic YouTube manifest matches need alignment before they can be advertised as built-in supported origins (issue #73). The app does not prove support for every media session, arbitrary website or browser simply by containing a manifest.

### 6.4 Canonicalization and source arbitration

**Target:** Correlate overlapping observations, merge duplicates and identify true replay/restart boundaries.

**Current:** Source-arbitration and playback-tracking implementations exist, with normalization and canonical play IDs. The behavior still needs multi-day real-player reconciliation; the design's completeness/freshness model is not a substitute for those results.

### 6.5 Eligibility

**Current domain rule:** artist and title must be present; duration must be known, positive and strictly greater than 30 seconds; exclusions, duplicate observations, existing provider actions, non-music and ads make a candidate ineligible. Threshold is `min(duration / 2, 240000 ms)`, calculated using integer milliseconds. Only confirmed played time counts. See [Scrobble Eligibility](../10_CoreEngine/SCROBBLE_ELIGIBILITY.md). Live/manual replay, pause/seek and long-session correctness still require actual playback testing.

### 6.6 Local history

**Current:** SQLite-backed playback observations and local-play records, query surfaces, history display and CSV export exist. The physical data model is documented in [Database Schema](../06_Data/DATABASE_SCHEMA.md).

**Not to assume:** arbitrary editing of historical display metadata, deleting one play/all history with complete related-action semantics, JSON export, or automated retention expiry are not established as complete current workflows without further source/test evidence.

### 6.7 Queue and synchronization

**Current:** A persistent SQLite provider-action queue supports scrobble, Now Playing, Love and Unlove operations; it has idempotency keys, leases, expiry recovery, bounded exponential retry, provider Retry-After handling, and explicit ambiguous-outcome state. Now Playing is ephemeral and stale non-in-flight items older than five minutes are expired.

The current retry implementation does not use full jitter or a general circuit breaker and the provider submits actions individually; do not describe jitter, provider batching, or guaranteed network-restoration triggers as implemented.

### 6.8 Diagnostics and support

**Current:** Local diagnostic/UI reason/status surfaces and a user-initiated diagnostics-only support ZIP are present. The bundle uses an allow-list and omits the listening database, credentials, artwork and free-form log messages. Windows Save/export behavior, seeded-canary redaction proof and accessibility remain qualification items.

**Target:** Expand actionable diagnostics only when the relevant states/telemetry are truly implemented and privacy reviewed. Do not claim every internal observation, state transition, provider attempt, or remote health metric is surfaced.

### 6.9 Statistics

**Current:** Local SQLite-backed summary/statistics surfaces exist. Each displayed number should make clear whether it refers to local/detected plays or provider-accepted scrobbles. Extensive visual analytics, reliability dashboards and top-artist/album reporting remain subject to checking the actual current UI and must not be inferred from a product proposal.

### 6.10 Loved tracks

**Current:** Love/Unlove actions are represented as separate provider actions and local loved-track records. User intent, pending state and provider acceptance must remain distinct.

### 6.11 Export, backup and restore

**Current:** CSV history export and validated SQLite backup/restore paths exist. The restore uses staged validation and pre-restore recovery/safety handling. Credentials and artwork cache are excluded from backup archives.

JSON export, complete settings export, retention/deletion workflows and every corruption/sidecar/rollback case remain unclaimed until implementation and manual evidence are recorded.

## 7. UX model

The implemented WinUI app has Home, History, Statistics, Loved, Queue, Sources, Diagnostics and Settings surfaces. Exact behavior and release qualification are documented in the corresponding UX specifications and real-world matrix. A rich mini-player, global shortcut, every proposed navigation/status state, and automatic sign-in behavior should be treated as target scope unless confirmed in current code.

The application should use native WinUI controls, respect system theme/accessibility settings, minimize intrusive notifications, and never show “scrobbled” before a provider-accepted or explicitly resolved result.

## 8. Privacy and licensing

Scrobblix is local-first and does not require its own cloud account. Application Last.fm credentials and the user session key belong in Windows Credential Locker rather than source control, extension storage or exported logs. The Browser Companion does not submit Last.fm requests directly and should not capture audio or unrelated browsing data. Before a public release, re-check Last.fm API terms/attribution, the product name/trademark, third-party software and media-artwork terms, and each browser store's current requirements. MIT code licensing does not license third-party marks/data.

## 9. External dependencies and trust boundaries

- Windows GSMTC/media-session availability for source apps.
- Last.fm API behavior and user-authorized account.
- Browser extension/native-host registration and actual extension IDs.
- .NET/Windows App SDK and SQLite/runtime libraries.
- Windows Credential Locker and filesystem/registry permissions.

A successful build does not prove that these external dependencies work on a target user's machine.

## 10. Release phases

- **Prototype / development:** domain logic, local store, desktop UI and development browser package.
- **Private-beta candidate:** exact commit passes Windows CI; required manual playback, provider, browser, IPC, recovery, privacy and support-bundle evidence is recorded; critical/high issues are addressed.
- **Public beta / v1.0:** supported environment, signed distribution/update strategy, data recovery, accessibility, licensing/privacy, dependency inventory/SBOM, support process and release gates are complete.

The current GitHub ZIP pipeline publishes **unsigned development previews**. It does not constitute signed production distribution.

## 11. Acceptance summary

Release acceptance requires requirements traceability to exact code/tests/evidence, no unresolved critical/high risk, correct multi-day playback and provider reconciliation, clean recovery behavior, tested browser integrations for any advertised origin, privacy/accessibility review, recoverable install/upgrade/uninstall, signed production distribution and release documentation. Unexecuted tests remain `NOT RUN`.

## 12. Change control

Update this PRD alongside meaningful behavior changes, but do not copy product targets into a “shipped features” list. Each implementation claim must cite the actual code/test path; each external or GUI qualification claim must cite exact candidate SHA and real evidence. Keep status distinctions visible in release materials.
