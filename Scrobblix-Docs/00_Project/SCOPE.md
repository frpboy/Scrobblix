# Scope

> **Project:** Scrobblix  
> **Status:** Implementation exists; private-beta acceptance in progress  
> **Last updated:** 2026-10-22  
> **Integration baseline:** `main`

## Current implementation snapshot

A code inspection of the current `main` snapshot confirms that Home, History/search/export, Statistics, Loved, Queue, Sources, Diagnostics and Settings surfaces exist. GSMTC tracking, Last.fm connection/actions, durable queue processing, metadata/artwork fallback, backup/restore logic, tray behavior, opt-in notifications and browser-companion development files are also present.

This is an implementation inventory, **not release certification**. Real Spotify/Last.fm reconciliation, repeat and restart correctness, sleep/wake, 8–24 hour soak, actual Chrome/Edge/Firefox delivery, Windows shell/accessibility, backup recovery, support bundle and signed install/update/rollback remain release gates. Check the exact commit and corresponding Actions run before treating a candidate as validated.

## v1 in scope

- Windows desktop companion with background/tray behavior.
- GSMTC event/poll source discovery and playback eligibility.
- Optional Chromium and Firefox Browser Companion.
- Native Messaging bridge, allow-lists and per-user registration/uninstall.
- Last.fm desktop authentication, Now Playing, scrobbling, Love and Unlove.
- Persistent offline queue with bounded retry and transparent provider outcomes.
- Source arbitration and duplicate prevention across GSMTC and extension events.
- Local history, search/filters/export, dashboard, statistics and queue diagnostics.
- Onboarding, source health, support bundle, settings, notifications and local backup/restore.
- Signed packaging, update verification, rollback and uninstall documentation.

## v1 explicitly out of scope

- System audio recording or acoustic fingerprinting.
- Streaming/downloading music or changing local media files.
- Cloud synchronization of local history.
- Social network, messaging, plugins or user scripting/rules.
- Mobile, Linux or macOS clients.
- Spotify Web API dependency.
- Automatic metadata changes that silently alter scrobble payloads.
- DRM/security bypass or bypassing Last.fm limits.

## Boundary rule

A feature enters scope only when its user value, privacy implications, failure behavior, storage impact, test plan and maintenance owner are explicit.
