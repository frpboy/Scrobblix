# Browser Companion Overview

> **Project:** Scrobblix  
> **Status:** Optional development integration; real browser qualification pending  
> **Updated:** 2026-10-22

## Role

The Browser Companion is an optional precision source for supported browser music players. It sends structured playback observations to the local Scrobblix desktop app over Native Messaging. It does not scrobble directly, hold Last.fm credentials, capture audio, or replace the Windows Global System Media Transport Controls (GSMTC) source.

For Spotify desktop, GSMTC remains the default path. The Browser Companion is optional unless the beta build explicitly advertises supported browser music sites.

## Browser status

- **Chrome:** Chromium Manifest V3 development bundle; unpacked installation and actual extension-ID registration must be tested on Windows.
- **Edge:** Chromium bundle, registered separately with the exact Edge extension ID. Chrome and Edge may use different IDs for the same unpacked source.
- **Firefox:** Separate Manifest V3 development manifest and temporary add-on flow; the temporary add-on is not a persistent signed release.
- **Brave, Opera, Vivaldi and other browsers:** Not claimed as supported until separately tested.

## Current observation path

1. A supported page adapter or conservative media observer detects metadata/state changes.
2. The extension validates sender origin and bounded metadata fields.
3. The service worker sends a versioned observation through Native Messaging.
4. The per-user native host validates caller identity/allow-list and forwards the event to the desktop's current-user named pipe.
5. The desktop validates the message and submits it to canonical playback tracking/source arbitration.

The companion must never be treated as healthy merely because files exist or the native host is registered. Sources should describe actual observed events and indicate when no event has been received.

## Detection and privacy

Detection uses site-specific adapters where implemented, Media Session metadata, and HTML media element state as fallback. Unknown or unsupported pages are diagnostic-only; the extension must not guess track data by broad page scraping. Only HTTPS artwork references are accepted. Raw browser page content, full URLs and Last.fm credentials are not part of the desktop support bundle.

## Qualification status

Static manifest/schema checks and scripted sender/payload tests pass on the private-beta readiness branch at its tested commit; these are not real browser tests. Manual acceptance remains **NOT RUN** for Chrome, Edge and Firefox installation, playback/pause/seek/skip delivery, extension reload, browser restart, host restart, uninstall and invalid-sender behavior. Record all live results in [the real-world manual test matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md).
