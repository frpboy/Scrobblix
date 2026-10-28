# Home Screen

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** UX specification with implementation boundary  
> **Last reviewed:** 2026-10-28



**Implementation status (2026-10-28):** The current WinUI Home presents current observed track/source, confirmed-played-time and eligibility progress, Last.fm/delivery status, a concise Today summary and three recent tracks. Routine status banners are intentionally suppressed. The layouts and state examples below are UX targets, not proof that every proposed control/state exists or has passed visual/accessibility testing. See the Feature Catalog and real-world manual test matrix.

## Purpose

Provide immediate confidence about current playback and system health.

## Wide layout

```text
┌ Navigation ─┬───────────────────────────────────────────────────────────┐
│ Home        │ Now playing                                               │
│ History     │ ┌─────────┐  Track title                                  │
│ Statistics  │ │ artwork │  Artist · Album                               │
│ Loved       │ └─────────┘  Chrome · YouTube Music · Browser Companion   │
│ Queue       │              Played 2:06 / threshold 2:30                  │
│ Diagnostics │              [███████████████────] 84%                     │
│ Sources     │              Waiting to scrobble                           │
│ Settings    │              [Love] [Open source] [More]                   │
│             ├───────────────────────────────────────────────────────────┤
│             │ Today          Queue          Last.fm       Sources        │
│             │ 42 tracks      2 pending      Connected     3 healthy      │
│             ├───────────────────────────────────────────────────────────┤
│             │ Recent activity / health recommendation                    │
└─────────────┴───────────────────────────────────────────────────────────┘
```

## Current playback card

Required fields:

- artwork with fallback monogram/music glyph;
- title, artist, album;
- playback source and detection method;
- source playback state;
- confirmed played duration;
- eligibility threshold and progress;
- provider state: not connected, now playing sent, eligible, queued, accepted, ignored, failed;
- metadata completeness warning;
- Love/Unlove, open provider/source, copy details, exclude source.

Media position and scrobble progress must not be conflated. Example: after a seek, source position may be 3:00 while confirmed played time is 0:45.

## Summary cards

- **Today:** detected plays, accepted scrobbles, listening time.
- **Queue:** pending/blocked/failed and oldest age.
- **Last.fm:** account, health, last successful call.
- **Sources:** active/healthy/attention count.

## Recent activity

Show the latest 5–10 plays with compact outcomes. A failed/ignored item with actionable reason ranks above ordinary successes.

## States

- Nothing playing: show last played item and “Listening for supported media.”
- Tracking paused: persistent pause banner and resume action.
- Local-only: explain Last.fm is not connected; continue history.
- Incomplete metadata: show exact missing fields and Diagnostics action.
- Multiple active sessions: show selected source plus switch/inspect action.
- Offline: “Eligible plays will be saved and retried.”


## Lightweight UI evolution

- Current playback, confirmed listening, eligibility and Last.fm delivery remain separate facts. Navigation layout must never reset or suspend tracking.
- The current track/source remains the Home focal point; provider/source marks are optional, local-only and always accompanied by visible text. Unknown sources stay neutral.
- The Today summary and three recent tracks remain concise. Queue, History and Statistics retain their detailed surfaces rather than being duplicated as decorative Home tiles.
