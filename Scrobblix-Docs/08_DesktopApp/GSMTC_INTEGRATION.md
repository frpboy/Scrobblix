# GSMTC Integration — Implementation Snapshot

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Implemented observation source; live-session qualification pending  
> **Last reviewed:** 2026-10-28

## Source of truth

The current adapter is `src/Scrobblix.Windows/GsmTcPlaybackObservationSource.cs`. It obtains the current session through `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`, reads media properties, playback info and timeline properties, then emits a structured `PlayObservation`.

Do not infer that every planned GSMTC feature (continuous session event subscriptions, all-session arbitration in the adapter, or every edge-case recovery path) is implemented merely because it appears in an older design list. Source, application ingestion and the manual matrix are the authoritative current state.

## Observation behavior

The current adapter:
- uses the Windows current media session and reads artist, title, album and other exposed metadata;
- records the source application identifier and a per-collector source instance identifier/sequence;
- captures timestamps through the injected wall-clock abstraction and maps GSMTC playback status into the application's playback-state model;
- uses the session timeline's end position/duration and current position as observation data, not proof by itself of listened time;
- normalizes artist/title for identity and reconciliation;
- attempts to cache supported artwork under the local Scrobblix artwork directory, capped at 8 MiB per stream and written through a temporary file;
- skips browser application thumbnails when they are likely to be a generic browser icon, allowing other metadata artwork sources or the UI placeholder to be used;
- treats missing session/media metadata as an expected unavailable/degraded condition rather than inventing track details.

## Confirmed played time and eligibility

A timeline position is evidence, not the sole played-time accumulator. Application/domain tracking maintains canonical play state and confirmed playing intervals. Seeking, pausing, repeated media notifications and overlapping source events must not count skipped or paused time as listening, nor produce duplicate provider actions. Eligibility is decided by the domain logic and covered by assertions, with actual Spotify/Last.fm behavior still requiring real-user-session testing.

## Browser interaction

GSMTC may identify a browser as the source application rather than a precise site or tab, and browser session artwork may be a generic icon. The optional Browser Companion supplies more precise metadata for supported web music experiences. Desktop GSMTC should remain functional when the extension is absent or disconnected.

## Diagnostics and failure behavior

The adapter may return no observation when no current session is available. Runtime/session errors, suspended or stale media state and missing metadata should be represented through safe diagnostics and explicit tracking status, not silently converted to valid track data. Keep external source values bounded and avoid logging full URLs or credentials.

## Qualification required

On the supported Windows version, test multiple supported media apps and Spotify desktop across:
- start/change/pause/resume/seek/end/repeat;
- no active session, unavailable metadata and rapid session transitions;
- sleep/resume, app restart and source restart;
- generic browser metadata/artwork followed by companion-provided data;
- a multi-day listening run with queue and Last.fm reconciliation.

Record exact app SHA, OS build, scenario result, and sanitized evidence in the [real-world manual test matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md). Unexecuted scenarios remain NOT RUN.
