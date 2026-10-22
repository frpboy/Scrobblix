# Scope

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Development implementation underway; release gates remain open  
> **Last updated:** 2026-08-01


## Current implementation snapshot

The development branch now contains the Home/History/Statistics/Loved/Queue/Sources/Diagnostics/Settings surfaces, source arbitration, durable queue processing, Last.fm action flows, artwork fallback, a backup/staged-restore path, a minimize-to-tray prototype, opt-in local Windows notifications with a separate track-detail privacy toggle, Chromium/Firefox companion manifests and an additional named-pipe peer check. See `docs/IMPLEMENTATION_RESUME.md` for the exact branch, last green CI baseline, and remaining manual release gates. These are development implementations; not all have been validated against a live Windows shell, real browser installation, or signed distribution.

## v1 in scope

- Windows 11 desktop application with background/tray operation.
- GSMTC session discovery and event-driven playback tracking.
- Optional Chromium and Firefox browser companion extensions.
- Native Messaging bridge installed with the desktop app.
- Last.fm desktop authentication, now-playing updates, scrobbling, love, and unlove.
- Persistent offline queue with bounded retry and batch submission.
- Source arbitration and duplicate prevention across GSMTC and extension events.
- Local listening history, timeline, search, filters, status, and export.
- Dashboard and local statistics derived from detected listening events.
- Queue management and full diagnostics.
- Startup configuration, settings, notifications, backup/restore, logs, and support bundles.
- Signed packaging and update verification.

## v1 explicitly out of scope

- Capturing or recording system audio.
- Acoustic fingerprinting or Shazam-like recognition.
- Streaming or downloading music.
- Modifying playback files or media libraries.
- Cloud synchronization of local history.
- Social network, messaging, or public profile hosting.
- Mobile, Linux, or macOS clients.
- AI-generated music recommendations.
- Automatic metadata correction that silently changes scrobble payloads.
- Bypassing website DRM, browser security, or Last.fm rate limits.
- Scrobbling video content merely because it contains an audio track.

## Later candidates

- ListenBrainz and Libre.fm providers.
- User-defined metadata transformation rules with previews and audit history.
- Plugin SDK for source adapters.
- Discord Rich Presence.
- Portable build with restricted extension integration.
- Cross-device encrypted sync designed as an independent opt-in service.

## Boundary rule

A feature enters scope only when its user value, privacy implications, failure behavior, storage impact, testing plan, and maintenance ownership are documented.
