# Compatibility Research Plan

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Test dimensions

- Windows release/build.
- Application/browser version.
- Packaged versus unpackaged application.
- Source type: desktop, PWA, browser tab, local file, stream, live radio.
- Metadata completeness.
- Timeline accuracy.
- Pause/seek/repeat behavior.
- Multiple sessions and audio focus.
- Sleep/resume and device lock.
- Browser extension enabled/disabled.

## Initial source matrix

| Source | Baseline path | Precision path | Required for v1 validation |
|---|---|---|---|
| Spotify Desktop | GSMTC | none/site APIs not required | Yes |
| Apple Music for Windows | GSMTC | none | Yes |
| VLC | GSMTC where exposed | optional player adapter later | Yes |
| MusicBee | GSMTC/plugin behavior | later plugin if required | Yes |
| foobar2000 | GSMTC/component-dependent | later component if required | Yes |
| Chrome/Edge YouTube Music | GSMTC | Browser Companion adapter | Yes |
| Chrome/Edge Spotify Web | GSMTC | Browser Companion adapter | Yes |
| Firefox YouTube Music | GSMTC varies | Browser Companion adapter | Yes |
| SoundCloud | GSMTC | Browser Companion adapter | Yes |
| Bandcamp | GSMTC varies | Browser Companion adapter | Yes |

## Evidence format

Each test stores synthetic/sanitized observations, expected canonical play, expected eligibility timeline, and expected terminal reason. Compatibility claims require reproducible fixtures and a documented environment.
