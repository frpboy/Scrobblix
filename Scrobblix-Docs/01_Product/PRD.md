# Product Requirements Document

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## 1. Product overview

Scrobblix detects music playback, tracks qualifying listening time, submits eligible plays to Last.fm, and preserves a local auditable listening record. It runs primarily in the Windows notification area and exposes a full management interface when the user needs history, statistics, queue control, settings, or diagnostics.

## 2. Problem

A missing Last.fm scrobble can originate at multiple layers: no source session, incomplete metadata, incorrect duration, pause/seek handling, source switching, duplicate suppression, sleep/resume, network failure, provider rejection, or expired authentication. Existing clients often collapse all of these into “nothing happened.” Users cannot trust or repair the pipeline.

## 3. Goals

- Detect supported desktop and browser playback with transparent source status.
- Implement Last.fm eligibility and submission correctly.
- Persist qualifying plays before attempting delivery.
- Prevent duplicate scrobbles across multiple detection inputs.
- Maintain searchable local history and useful statistics.
- Provide exact human-readable reasons for every non-success outcome.
- Operate with low background CPU, memory, disk, and network usage.
- Protect credentials and private listening data.

## 4. Non-goals

See [`../00_Project/SCOPE.md`](../00_Project/SCOPE.md). Scrobblix is not an audio-recognition service, music player, streaming platform, cloud account service, or DRM bypass.

## 5. Primary users

- Browser-first Last.fm listener.
- Multi-player power user.
- Privacy-conscious local-history user.
- Troubleshooter/developer.

Detailed personas are in [`PERSONAS.md`](PERSONAS.md).

## 6. Product capabilities

### 6.1 Onboarding

The first-run experience explains local processing, detects available media sessions, offers Browser Companion installation, authenticates Last.fm through the user's browser, tests the connection, and asks whether to launch at sign-in. It must permit a “local history only” mode without Last.fm authentication.

### 6.2 Current playback

Home shows artwork when available, title, artist, album, source application/site, detection method, playback status, elapsed time, duration, eligibility threshold, played-time progress, and expected scrobble state. Missing fields are explicit rather than hidden.

### 6.3 Detection

GSMTC discovers all sessions and follows session-list, current-session, media-property, timeline, and playback-info changes. The Browser Companion identifies supported sites and observes media elements/Media Session metadata through site adapters. Users can disable applications, websites, or specific source paths.

### 6.4 Canonicalization and arbitration

The engine normalizes whitespace and Unicode safely, separates artist/title only when an adapter supplies a trusted rule, correlates overlapping observations, and selects a canonical source by deterministic completeness/freshness rules. Raw observations remain inspectable for diagnostics.

### 6.5 Eligibility

A track longer than 30 seconds becomes eligible after half its duration or four minutes, whichever occurs first. Only confirmed played intervals count. Pauses, seeks, replay, loops, unknown durations, live streams, advertisements, and track restarts are addressed by explicit policies.

### 6.6 Local history

Every meaningful candidate can be retained locally with source, metadata, timestamps, played duration, eligibility, submission status, reason codes, and diagnostic correlation. Users can filter, search, edit local display metadata, delete records, and export selected ranges.

### 6.7 Queue and synchronization

Eligible submissions are queued durably. The queue distinguishes pending, scheduled retry, in-flight, accepted, provider-ignored, authentication-blocked, permanent failure, and user-cancelled states. Transient failures use bounded exponential backoff with jitter and network restoration triggers. Cached scrobbles batch up to provider limits.

### 6.8 Diagnostics

Diagnostics displays source health, current raw observations, canonical play, metadata completeness, state-machine status, eligibility calculation, duplicate match, queue state, authentication, network/API health, database integrity, extension/host status, and recommended actions.

### 6.9 Statistics

Statistics derive from local listening history and clearly indicate whether they represent detected plays, eligible plays, or accepted provider scrobbles. Views include today/week/month/year totals, top artists/albums/tracks, listening-time distribution, heatmap, sessions, sources, and reliability rate.

### 6.10 Loved tracks

Users can love/unlove the current or historical track. The action is queued separately from scrobbling, shows remote status, and never falsely reports success before provider acknowledgement.

### 6.11 Export, backup, and recovery

History exports to CSV and JSON with schema/version metadata. Full backup includes database and settings but excludes recoverable caches and secrets unless an explicit encrypted export mechanism is introduced. Restore validates version and integrity before replacement.

## 7. UX model

The desktop application uses a left navigation rail:

- Home
- Timeline/History
- Statistics
- Loved
- Queue
- Diagnostics
- Browsers & Sources
- Settings

The app defaults to dark mode only if it matches Windows settings; it does not force dark mode. The mini-player/tray flyout provides current track, eligibility progress, love, pause Scrobblix, retry queue, and open-app actions without becoming a full music controller.

## 8. Business and licensing

Scrobblix is initially non-commercial and open source under MIT. The app must comply with Last.fm API terms, provide required attribution where Last.fm data is displayed, and avoid implying endorsement. Any monetization, sponsorship, paid cloud service, or commercial distribution requires a separate legal/API review.

## 9. Dependencies

- Last.fm API availability and API account.
- Windows media-control integration by source apps.
- Browser extension store approval or manual installation.
- Microsoft Windows App SDK and .NET runtime/tooling.
- SQLite library and OS credential storage.

## 10. Release phases

- **Prototype:** deterministic engine, synthetic observations, local database.
- **Alpha:** GSMTC + Last.fm sandbox/test account + basic tray UI.
- **Beta:** Browser Companion, diagnostics, signed package, migration/recovery.
- **v1.0:** qualification targets, documentation, accessibility, privacy, and support readiness met.

## 11. Acceptance summary

v1.0 is accepted only when required functional requirements pass, no critical/high security issue is open, supported sources meet reliability targets, the installer/update flow is signed and recoverable, and a user can diagnose representative missed-scrobble scenarios through the UI.
