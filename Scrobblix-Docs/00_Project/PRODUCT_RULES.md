# Product Rules

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


These rules are normative. An implementation change that violates one requires an explicit product decision and ADR.

## Scrobbling rules

- A track is eligible only when its duration is greater than 30 seconds and it has played for at least half its duration or four minutes, whichever occurs earlier.
- Played time is accumulated only while playback is positively known to be playing.
- Paused, stopped, buffering, seeking, and unknown intervals do not automatically count as played time.
- The original track-start timestamp is retained through queue retries.
- An eligible event is persisted before a remote submission attempt can cause it to be lost.
- A Last.fm `track.updateNowPlaying` failure is logged but is not retried as a stale now-playing event.
- Cached scrobbles may be submitted in batches of at most 50.
- Metadata corrections returned by Last.fm are never fed back into a scrobble payload unless the user explicitly approves that behavior.

## Detection rules

- GSMTC is the baseline source.
- Browser extension metadata may supersede matching GSMTC metadata when it is fresher and more complete.
- Competing sources for the same playback must merge into one canonical play, not two scrobbles.
- The user can inspect which source won and why.
- Audio fingerprinting is prohibited in v1.

## Data rules

- Listening history is local by default.
- Telemetry is independent from local history and disabled by default.
- Secrets are stored through Windows Credential Locker or an equivalent OS-protected mechanism.
- Logs are structured, rotated, redacted, and do not contain session keys or API secrets.
- Data deletion removes derived statistics and cached artwork associated with the deleted records where practical.

## UX rules

- Home shows current playback, progress to eligibility, source, Last.fm state, queue health, and today's summary without navigation.
- Diagnostics must provide a human-readable recommendation, not only an internal code.
- Destructive actions require confirmation when recovery is impossible.
- Keyboard navigation, screen-reader labels, visible focus, reduced motion, and high-contrast behavior are release requirements.

## Release rules

- Public builds are signed.
- Update manifests and packages are verified before installation.
- No release is published with failing required tests, unresolved critical/high security findings, destructive migration without recovery, or undocumented permissions.
