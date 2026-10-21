# Browser Companion Overview

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Role

The Browser Companion is an optional precision layer for browser music. It does not scrobble directly, hold Last.fm credentials, or replace the desktop engine. It emits structured local playback observations.

## Supported browsers

- Chromium family: Chrome and Edge required; Brave/Opera/Vivaldi best-effort after testing.
- Firefox required with a WebExtension-specific manifest/build.

## Detection tiers

1. Site-specific adapter with tested selectors/events/API semantics.
2. Standard Media Session metadata.
3. HTML `<audio>`/`<video>` element state with conservative metadata.
4. Unsupported/diagnostic only; no broad page scraping guesses.

## User value

Exact site/tab identity, better artist/title/album/duration/position, advertisement/media-kind hints, and faster state events. GSMTC remains available when extension is absent.
