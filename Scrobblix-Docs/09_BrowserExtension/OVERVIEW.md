# Browser Companion Overview

> **Project:** Scrobblix  
> **Status:** Optional development integration; real browser qualification remains pending  
> **Last reviewed:** 2026-10-28

## Role

The Browser Companion is an optional precision source for supported browser music players. It sends bounded structured playback observations to the local Scrobblix desktop app over Browser Native Messaging. It does not scrobble directly, hold Last.fm credentials, capture audio, or replace Windows Global System Media Transport Controls (GSMTC).

For Spotify desktop, GSMTC remains the default path. The Browser Companion is optional unless a release explicitly advertises support for a browser music site.

## Browser support status

- **Chrome:** Chromium Manifest V3 development bundle; unpacked installation and actual extension-ID registration require real-Windows testing.
- **Edge:** Chromium bundle registered separately with the Edge extension ID; do not assume its ID matches Chrome.
- **Firefox:** Separate Manifest V3 development manifest and temporary add-on flow; a temporary add-on is not a persistent signed release.
- **Brave, Opera, Vivaldi and others:** Not claimed as supported until separately tested.

## Current observation path

1. A supported page adapter/media observer reads published media metadata and playback state.
2. The extension validates the sender origin and bounded metadata fields.
3. The service worker sends a supported observation via Native Messaging.
4. The per-user native host validates caller identity/allow-list and forwards the payload to the desktop's current-user named pipe.
5. The desktop validates the message and routes it through observation ingestion and source arbitration.

The currently implemented host protocol accepts `ping.v1` and `playback.observation.v1`. Older proposed message types such as `hello.v1`, `playback.snapshot.v1`, or sequence acknowledgements must not be treated as deployed protocol capabilities.

## Detection and privacy

Detection uses the current supported-site observer/adapters and the media information published by a browser page; data may be incomplete. Unknown or unsupported pages must not lead to guessed track metadata from arbitrary DOM/page scraping. Only supported metadata is sent to the local desktop process. The extension does not send listening data to Scrobblix-operated servers or embed Last.fm credentials.

Built-in site permissions and user-granted optional site access are distinct. Users must be able to revoke added-site access via browser controls; the desktop support bundle should not include raw browsing history, full site URLs or tab identity.

## Qualification status

Automated source/security tests, manifest validation and extension build checks are part of Windows CI. They do not validate real installed-browser permissions, native host launch, OS registration, actual media events or uninstall cleanup. Manual acceptance remains `NOT RUN` for each advertised browser until recorded in the [real-world test matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md).

**Origin consistency note (tracked in [issue #73](https://github.com/frpboy/Scrobblix/issues/73)):** The manifests include generic YouTube host matches, but the code-level built-in sender allow-list currently only includes `https://music.youtube.com` and `https://open.spotify.com`. Generic `youtube.com` / `www.youtube.com` observations are not implicitly trusted as built-in origins and may be rejected unless explicitly registered as a custom site. Do not advertise those generic origins as natively supported until the manifest and validation layers are reconciled and tested.
