# Native Messaging Host

> **Status:** Chromium + Firefox development implementation; release security review and browser-side qualification remain open

## Executable role and framing

`Scrobblix.NativeHost.exe` reads length-prefixed JSON from stdin, caps frames at 256 KiB, validates the protocol version/message envelope and forwards only supported playback observation messages to the desktop app's current-user named pipe. Native-messaging stdout is reserved for bounded protocol responses. The host does not contain Last.fm credentials.

## Browser identity

- **Chrome/Edge:** the native manifest uses `allowed_origins` with the exact Chromium extension origin. The same 32-character extension ID must be present in `AllowedChromeExtensionIds` under `HKCU\Software\Scrobblix\NativeMessaging`.
- **Firefox:** `browser-extension/manifest.firefox.json` is a separate Manifest V3 development manifest, with Gecko add-on ID `scrobblix@frpboy.in`. Firefox manifests use `allowed_extensions`, and Windows registration is under `HKCU\Software\Mozilla\NativeMessagingHosts\com.frpboy.scrobblix`. The ID must match the manifest, installer allow-list and running extension. A signed/distributed add-on must use the stable ID belonging to that signed package.

Run `scripts/install-native-host.ps1` after publishing the host. The script can register Chrome/Edge using `-ExtensionId` and can register Firefox-only using `-RegisterFirefox -FirefoxExtensionId "scrobblix@frpboy.in"`. Use `scripts/uninstall-native-host.ps1` to remove registration. Restart a registered browser after changes.

## Desktop pipe peer check

The desktop pipe is current-user-only and now rejects a connected peer unless Windows reports the client image as the installed `%LOCALAPPDATA%\Scrobblix\NativeHost\Scrobblix.NativeHost.exe`, and the native host's parent image is named `chrome.exe`, `msedge.exe` or `firefox.exe`. The check runs before any incoming message bytes are consumed and the rejection reason is logged.

This is additional defense-in-depth, **not cryptographic process identity**. It does not replace a signed native host, controlled installation directory ACLs, a stronger process/package publisher check, race/handle review, or a formal threat assessment. A same-user attacker may tamper with per-user files/registration; do not claim production-grade local IPC authentication on the basis of the path/parent-name check alone.

## Remaining validation

- Install and test the unpacked companion in Chrome, Edge and Firefox on Windows with the actual registered extension IDs.
- Verify malformed/oversized frames, allow-list failure, host unavailable, desktop unavailable, browser restart, Firefox temporary add-on reload and uninstall cleanup.
- Firefox source/manifest/type/build validation is included in CI, but a live Firefox browser install has not been claimed as completed.
- Signed distributable host packaging, installer rollback, support for host upgrades and formal security review remain release gates.
