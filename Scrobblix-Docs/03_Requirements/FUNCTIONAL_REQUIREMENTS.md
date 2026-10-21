# Functional Requirements

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


Priority: **M**ust, **S**hould, **C**ould, **L**ater.

## FR-A — Application lifecycle and onboarding

| ID | Requirement | Priority |
|---|---|---:|
| FR-A001 | The app shall initialize storage, settings, logging, source adapters, queue recovery, and health checks before reporting itself ready. | M |
| FR-A002 | First run shall explain local history, Last.fm submission, Browser Companion, permissions, and telemetry defaults. | M |
| FR-A003 | A user shall be able to use local-history mode without connecting Last.fm. | M |
| FR-A004 | The app shall support launch at Windows sign-in with a user-controlled setting. | M |
| FR-A005 | Closing the main window shall follow the configured close behavior: minimize to tray, prompt, or exit. | M |
| FR-A006 | Exiting shall flush state transactionally without waiting indefinitely for network operations. | M |
| FR-A007 | The app shall reconcile sleep, resume, lock, unlock, clock changes, and abrupt termination. | M |

## FR-B — Last.fm account and provider

| ID | Requirement | Priority |
|---|---|---:|
| FR-B001 | The app shall authenticate through the Last.fm desktop/browser authorization path without collecting the user's password. | M |
| FR-B002 | The resulting session key shall be stored in OS-protected credential storage. | M |
| FR-B003 | Users shall be able to test, disconnect, and reconnect the account. | M |
| FR-B004 | The app shall send now-playing promptly for a new canonical track when authenticated and online. | M |
| FR-B005 | Failed now-playing updates shall be logged and shall not be replayed as stale events. | M |
| FR-B006 | The app shall submit eligible scrobbles with the original UTC start timestamp. | M |
| FR-B007 | Cached scrobbles shall be batched up to the provider maximum of 50. | M |
| FR-B008 | The app shall classify provider responses into accepted, ignored, transient, authentication, rate-limit, and permanent outcomes. | M |
| FR-B009 | Users shall be able to love and unlove a track, with remote status visible. | S |
| FR-B010 | The app shall respect rate limits and never attempt to circumvent them. | M |

## FR-C — GSMTC detection

| ID | Requirement | Priority |
|---|---|---:|
| FR-C001 | The app shall request and monitor the GSMTC session manager. | M |
| FR-C002 | It shall observe session additions/removals and current-session changes. | M |
| FR-C003 | It shall read media properties, playback status, timeline position, duration, and source application identifier when available. | M |
| FR-C004 | It shall subscribe to source events and use bounded reconciliation polling only where event reliability is insufficient. | M |
| FR-C005 | Users shall be able to include/exclude applications. | M |
| FR-C006 | Source health shall expose last observation time, metadata completeness, state, and errors. | M |

## FR-D — Browser Companion

| ID | Requirement | Priority |
|---|---|---:|
| FR-D001 | The extension shall support current Chromium Manifest V3 and a Firefox WebExtension build. | M |
| FR-D002 | It shall detect supported media using site adapters, `navigator.mediaSession`, and HTML media elements as permitted. | M |
| FR-D003 | It shall communicate only with the registered Scrobblix Native Messaging host. | M |
| FR-D004 | It shall request minimal permissions and support optional per-site grants. | M |
| FR-D005 | It shall identify browser, profile where safely possible, tab, site adapter, media element, playback state, position, duration, and metadata. | M |
| FR-D006 | It shall survive service-worker suspension and reconnect without duplicating plays. | M |
| FR-D007 | The desktop app shall display extension version, host status, permissions, connected tabs, and adapter health. | M |
| FR-D008 | Unsupported sites shall fail safely without scraping unrelated page content. | M |

## FR-E — Playback model

| ID | Requirement | Priority |
|---|---|---:|
| FR-E001 | Every source report shall become an immutable observation with source and capture timestamps. | M |
| FR-E002 | Observations shall be normalized without silently inventing required metadata. | M |
| FR-E003 | Matching observations shall be correlated into one canonical play. | M |
| FR-E004 | The engine shall maintain explicit states for candidate, playing, paused, unknown, eligible, ended, abandoned, ignored, duplicate, and queued/submitted outcomes. | M |
| FR-E005 | Played time shall accumulate from monotonic intervals positively identified as playing. | M |
| FR-E006 | A track shall become eligible only under documented Last.fm criteria. | M |
| FR-E007 | Seek, restart, loop, metadata change, source handoff, and duration correction shall follow deterministic policies. | M |
| FR-E008 | Every terminal non-success state shall have a stable reason code and localized explanation. | M |

## FR-F — Queue and synchronization

| ID | Requirement | Priority |
|---|---|---:|
| FR-F001 | An eligible scrobble shall be persisted atomically with its history state before delivery. | M |
| FR-F002 | Queue state shall survive crash, power loss, update, and network outage. | M |
| FR-F003 | Transient failures shall retry with exponential backoff, jitter, and upper bounds. | M |
| FR-F004 | Authentication failures shall block affected provider actions until reauthentication. | M |
| FR-F005 | Users shall be able to retry, cancel, inspect, and filter queue items. | M |
| FR-F006 | Retry All shall respect rate limits and not create concurrent duplicates. | M |
| FR-F007 | The system shall use idempotency/duplicate keys locally even though the provider API lacks a client idempotency token. | M |

## FR-G — History and statistics

| ID | Requirement | Priority |
|---|---|---:|
| FR-G001 | The app shall store local records for detected candidate plays according to retention settings. | M |
| FR-G002 | History shall support date, text, artist, album, source, application/site, eligibility, and provider-status filters. | M |
| FR-G003 | A detail view shall show metadata, timing, observations, state transitions, reason, queue link, and provider response summary. | M |
| FR-G004 | Users shall be able to delete selected local records and clear all local history. | M |
| FR-G005 | Statistics shall be calculated from local records with definitions visible. | S |
| FR-G006 | The app shall provide timeline, daily/weekly/monthly/yearly summaries, top entities, heatmap, and source/reliability breakdown. | S |
| FR-G007 | Exports shall support CSV and JSON with versioned schema. | M |

## FR-H — UI, tray, notifications, and settings

| ID | Requirement | Priority |
|---|---|---:|
| FR-H001 | Home shall summarize current playback, eligibility, source health, provider health, queue, and today's listening. | M |
| FR-H002 | The tray menu shall expose now playing, open app, pause/resume Scrobblix, love, retry queue, settings, and exit. | M |
| FR-H003 | Notifications shall be individually configurable for scrobble success, failure, auth, queue recovery, and updates. | S |
| FR-H004 | Settings shall be searchable and grouped by General, Account, Detection, Browser, Scrobbling, History, Notifications, Privacy, Updates, and Advanced. | M |
| FR-H005 | Settings validation and restart requirements shall be explicit. | M |
| FR-H006 | The UI shall provide light, dark, system, high-contrast, and reduced-motion-compatible behavior. | M |

## FR-I — Diagnostics, backup, and recovery

| ID | Requirement | Priority |
|---|---|---:|
| FR-I001 | Diagnostics shall display health for app, database, GSMTC, extension/host, provider, network, queue, notifications, and updater. | M |
| FR-I002 | A live inspector shall show sanitized source observations and canonicalization decisions. | M |
| FR-I003 | Users shall be able to create a previewable, redacted support bundle. | M |
| FR-I004 | The app shall run database integrity checks and provide recovery options. | M |
| FR-I005 | Users shall be able to back up and restore local data with compatibility validation. | M |
| FR-I006 | The app shall provide a repair action for Native Messaging registration. | M |
| FR-I007 | The app shall expose version/build/dependency/license information. | M |
