# Source Notes

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## User-provided design basis

The initial concept established:

- Windows media-session detection as the primary path.
- Optional browser extension for greater accuracy.
- Last.fm now-playing and scrobble APIs.
- SQLite offline queue and retry.
- tray application, history, statistics, loved tracks, diagnostics, browser status, settings, notifications, search, and health dashboard.
- a modern Windows 11/Fluent-influenced interface.

## External verification incorporated into this baseline

- Last.fm recommends `track.updateNowPlaying` and `track.scrobble`; now-playing is optional and should be sent when listening starts.
- Last.fm eligibility is track length over 30 seconds plus half duration or four minutes, whichever occurs first.
- Last.fm supports cached scrobble batches up to 50.
- Microsoft recommends WinUI 3/Windows App SDK for new native Windows desktop applications.
- GSMTC exposes playback sessions integrated with Windows system media controls.
- Chromium Manifest V3 uses service workers and supports Native Messaging.
- Firefox WebExtensions are broadly compatible with Chromium extension APIs and support Native Messaging.
- Windows Credential Locker is available for protected credential storage.

Official URLs and access dates are maintained in [`../references/OFFICIAL_SOURCES.md`](../references/OFFICIAL_SOURCES.md).
