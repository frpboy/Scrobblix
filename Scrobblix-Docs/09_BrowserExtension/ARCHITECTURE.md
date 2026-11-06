# Browser Companion Architecture — Implementation Snapshot

> **Project:** Scrobblix  
> **Status:** Development integration; real browser qualification remains pending  
> **Last reviewed:** 2026-10-28

## Implemented components

- Chromium Manifest V3 extension with a TypeScript service worker, content/media observer, popup, manifest checks and build output under `dist`.
- A separate Firefox development manifest with an explicit add-on identity.
- Source-origin and playback-payload validation, bounded text/number values, and user-controlled optional access for additional HTTP/HTTPS media sites.
- Browser Native Messaging host that validates caller identity and supported message type before forwarding supported observations to the desktop named pipe.
- Desktop-side IPC checks, source arbitration and playback tracking.

The current Native Messaging host recognizes `ping.v1` and `playback.observation.v1`; it does not implement the older design's general hello/snapshot/acknowledgement protocol. See [Native Messaging Protocol](../07_Interfaces/NATIVE_MESSAGING_PROTOCOL.md) for the exact current boundary.

## Data flow

```text
Supported / user-authorized browser page
  -> content observer (published media metadata and state)
  -> extension sender / payload validation
  -> browser Native Messaging framing
  -> per-user native host (identity + size + envelope checks)
  -> current-user named pipe
  -> desktop ingress validation
  -> source arbitration / canonical playback tracking
  -> local history and eligible provider-action queue
```

The extension does not contact Last.fm, hold Last.fm credentials, capture system audio, or replace desktop GSMTC. Provider delivery, local credential storage, queue recovery and provider acceptance remain in the desktop app.

## Manifest and origin model

- Chromium uses Manifest V3 `background.service_worker`; do not add legacy `background.scripts`.
- The required host permissions cover the documented built-in sites in the manifest.
- The extension declares optional HTTP/HTTPS host permission patterns so the user can add another site with an explicit browser grant. The extension must not use that optional permission to silently scrape arbitrary sites.
- Chrome and Edge can provide different unpacked extension IDs. Register the actual ID from each browser separately.
- Firefox uses its own development manifest/add-on ID and separate host registration.
- The current source defaults to no automatic audio capture and no broad background history collection.

## Source identity and state

Browser observations are additional metadata events, not proof that a remote scrobble was accepted. Source status must be based on observed events and valid state transitions, not file presence or registry registration alone. Cross-source reconciliation with GSMTC, duplicate suppression and behavior after worker/browser restart require real-machine tests.

## Security and residual risk

The native host checks the declared extension identity against the per-user registry allow-list before processing messages. A current-user-only named pipe and desktop checks for native-host executable path/browser parent process are additional safeguards, not cryptographic publisher identity. Per-user registry/file tampering, extension identity handling, same-user IPC, signature/provenance and store supply-chain risks remain part of the threat model.

## Build and validation

From the repository root:

```powershell
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/test-browser-security.mjs
node scripts/validate-browser-manifests.mjs
node scripts/validate-docs.mjs
```

Windows CI validates compile/source contracts and packages a development companion bundle. It does not prove actual Chrome, Edge or Firefox event delivery. Installed-browser permissions, exact IDs, play/pause/seek, extension reload, host unavailable/recovery, uninstall and no-duplicate behavior remain manual acceptance cases and stay `NOT RUN` until executed.

Built-in browser origins (issue #73): the v1 built-in YouTube adapter is YouTube Music (`https://music.youtube.com`), alongside Spotify Web (`https://open.spotify.com`). Generic `https://youtube.com` and `https://www.youtube.com` pages are intentionally excluded from fixed content-script matches and required built-in host permissions. A user may add a standard YouTube origin through the custom-site flow; forwarding then requires both the exact stored registration and the browser's grant for that origin. Standard YouTube videos may not publish artist metadata and must not be described as built-in support.
