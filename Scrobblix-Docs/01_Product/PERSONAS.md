# Personas

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## P1 — Browser-first listener

**Profile:** Uses YouTube Music, Spotify Web, SoundCloud, Bandcamp, or niche streaming sites. Rarely opens a desktop music player.

**Needs:** Accurate tab metadata, no duplicate scrobbles, clear extension status, privacy assurance, automatic operation.

**Pain:** System media controls sometimes show a video title, channel name, advertisement, or stale tab.

**Success:** Can install the companion, enable selected sites, and confirm the exact tab/source on Home.

## P2 — Multi-player power user

**Profile:** Switches among Spotify Desktop, MusicBee, foobar2000, VLC, and browser playback. May have two sessions active.

**Needs:** Deterministic active-source selection, exclusions, history filters, queue inspection, detailed logs.

**Pain:** Legacy clients attach to only one player or double-count when integrations overlap.

**Success:** One real play creates one canonical history record and at most one scrobble.

## P3 — Listening archivist

**Profile:** Treats listening history as a personal journal and wants long-term exportability.

**Needs:** Local history, backups, rich metadata, timeline, CSV/JSON export, retention control.

**Pain:** Provider history can be corrected, unavailable, or difficult to analyze.

**Success:** Can export a date range with stable schema and understand local versus provider state.

## P4 — Privacy-conscious user

**Profile:** Wants reliable scrobbling but distrusts broad browser permissions and telemetry.

**Needs:** Local processing, exact permission explanation, site opt-in, no audio capture, easy data deletion.

**Pain:** Extensions often request access to all sites without explaining why.

**Success:** Can run GSMTC-only, grant extension permissions per site, and inspect what data is transmitted locally/remotely.

## P5 — Troubleshooter/developer

**Profile:** Comfortable with technical details and may contribute adapters or bug reports.

**Needs:** Raw observation viewer, correlation IDs, reason codes, sanitized support bundles, reproducible fixtures.

**Pain:** “Not scrobbling” reports lack enough evidence to fix.

**Success:** Can identify whether the fault lies in source exposure, adapter parsing, state tracking, queue, auth, or provider response.

## P6 — Casual Last.fm user

**Profile:** Wants a trustworthy set-and-forget tray app.

**Needs:** Simple onboarding, sensible defaults, quiet notifications, automatic recovery.

**Pain:** Technical clients expose too many settings before working.

**Success:** Authenticates, sees a green health state, and rarely opens advanced pages.
