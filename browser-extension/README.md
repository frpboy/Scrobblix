# Scrobblix Browser Companion

The companion observes media metadata and playback-state changes in supported browser tabs, then forwards bounded metadata through Native Messaging to the local Scrobblix desktop app. It does not capture audio, hold Last.fm credentials, or contact Last.fm itself. The companion is optional for Spotify desktop when Windows GSMTC already provides adequate metadata.

## Build and static validation

From the repository root:

```powershell
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/test-browser-security.mjs
node scripts/validate-browser-manifests.mjs
node scripts/validate-docs.mjs
```

These commands validate source/build contracts. They do **not** prove that an installed browser sends real playback events or that the native host works on your Windows profile.

## Chromium: Chrome and Edge

1. Open `chrome://extensions` or `edge://extensions`.
2. Enable Developer mode and choose **Load unpacked**.
3. Load the `browser-extension` directory.
4. Copy the actual extension ID shown by Chrome.
5. Repeat steps 1–4 in Edge. Do not assume its extension ID is the same.

Publish the native host:

```powershell
dotnet publish src/Scrobblix.NativeHost/Scrobblix.NativeHost.csproj --configuration Release --output artifacts/Scrobblix/native-host
```

If Chrome and Edge use different IDs, register both. Replace these placeholders with the IDs shown by your browsers:

```powershell
.\scripts\install-native-host.ps1 -RegisterChrome -RegisterEdge -ExtensionId @("CHROME_EXTENSION_ID", "EDGE_EXTENSION_ID") -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host"
```

When testing only one browser, pass its actual ID and only that browser's switch, for example:

```powershell
.\scripts\install-native-host.ps1 -RegisterChrome -ExtensionId "CHROME_EXTENSION_ID" -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host"
```

The installer installs per user: it copies the published host under `%LOCALAPPDATA%\Scrobblix\NativeHost`, creates HKCU registrations, and stores the configured ID allow-list under `HKCU\Software\Scrobblix\NativeMessaging`. Restart the registered browsers and Scrobblix after installation.

## Companion controls

Click the Scrobblix toolbar icon to open the companion panel. It can pause or resume browser tracking, test whether the registered native host replies, and add the current HTTP/HTTPS site. Adding a site requests access only for that site and uses its published media-session or media-element metadata; it does not invent song details. Reload the site once after adding it. Remove a custom site from the same list to revoke the granted permission.

The panel can confirm that the native host is reachable. It cannot claim that the desktop app is running because the native protocol deliberately does not expose that state.

## Firefox

The development add-on ID is `scrobblix@frpboy.in` and the Firefox manifest uses Manifest V3 background scripts rather than Chrome's service-worker-only field.

1. Build the companion using the commands above.
2. Copy the `browser-extension` folder to a separate directory, such as `browser-extension-firefox-test`.
3. Replace the copied `manifest.json` with `manifest.firefox.json`, naming the copy `manifest.json`; keep `dist` beside it.
4. In Firefox, open `about:debugging#/runtime/this-firefox`, choose **Load Temporary Add-on**, and select the copied manifest.
5. Publish the native host as shown above and register Firefox alone:

```powershell
.\scripts\install-native-host.ps1 -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host" -RegisterFirefox -FirefoxExtensionId "scrobblix@frpboy.in"
```

For a signed/distributed Firefox extension, the stable ID in the signed package, host manifest and Windows allow-list must match. Firefox uses `allowed_extensions`; Chromium uses `allowed_origins`.

## Message validation and security boundary

The service worker accepts content-script messages only from recognized built-in origins or a browser-granted custom origin, validates the playback payload and bounds its fields, and derives origin/tab identity from the browser-provided sender rather than trusting page-controlled values. The native host validates the declared extension identity against the registered per-user allow-list **before handling message types, including ping**. Frames are length-prefixed and capped at 256 KiB.

Built-in browser origins (issue #73): the v1 built-in YouTube adapter is YouTube Music (`https://music.youtube.com`), alongside Spotify Web (`https://open.spotify.com`). Generic `https://youtube.com` and `https://www.youtube.com` pages are intentionally excluded from fixed content-script matches and required built-in host permissions. A user may add a standard YouTube origin through the custom-site flow; forwarding then requires both the exact stored registration and the browser's grant for that origin. Standard YouTube videos may not publish artist metadata and must not be described as built-in support.

Built-in sites use extension-granted origin access. A user-added HTTP/HTTPS site is accepted only after the browser grants that exact origin; Scrobblix reads published media-session or media-element metadata and never invents missing fields. Standard YouTube videos can expose incomplete artist metadata, so Scrobblix tracks only metadata the page actually publishes.

When a user removes a custom site, the popup first revokes its service-worker authorization and removes the local registration before unregistering the script or revoking the browser permission. The service worker revalidates the exact permission and latest registration after asynchronous checks, then repeats the authorization gate after opening the native port immediately before posting, so an in-flight custom-site observation is discarded after revocation. Only the extension popup may request this removal operation.

The desktop pipe is current-user-only and has additional client/process checks, but these are defense-in-depth—not cryptographic proof of a signed executable. Same-user file/registry tampering and process-identity hardening remain in the security review.

## Uninstall

After closing the browsers, remove the extension/add-on. Then run:

```powershell
.\scripts\uninstall-native-host.ps1
```

The script removes Chrome, Edge and Firefox HKCU registration and trusted-ID entries, then attempts to delete the dedicated per-user NativeHost directory. If a host process locks a file, it warns that residual files may need removing after related processes close. Listening history and logs are not deleted.

## Real-browser qualification — still required

Test each advertised browser separately on Windows:
- Track change, play, pause, resume, seek and end on a supported player.
- Browser/extension reload, browser restart, native host restart/unavailable, and desktop app restart.
- Actual Chrome and Edge IDs, including a different-ID case.
- Firefox temporary add-on reload/restart and exact add-on identity.
- Wrong/untrusted identity, forged ping, malformed/oversized frame, unsupported origin and invalid payload.
- Uninstall registration/files and verify no stale listener or event delivery remains.

Record results in the [Real-world Manual Test Matrix](../Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md). These tests remain `NOT RUN` until executed on the real environment.
