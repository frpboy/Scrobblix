# Native Messaging Host

> **Status:** Development implementation with automated checks; real-browser qualification and formal security review remain open
> **Updated:** 2026-10-28

## Executable role and framing

`Scrobblix.NativeHost.exe` reads length-prefixed JSON from stdin, caps frames at 256 KiB, validates the protocol version/message envelope and forwards supported playback-observation messages to the desktop app's current-user named pipe. Native-messaging stdout is reserved for protocol responses; diagnostic logging must not be written to stdout. The host does not contain Last.fm credentials.

## Browser identity and per-user registration

- **Chrome/Edge:** The native manifest uses `allowed_origins` with every explicitly supplied extension origin. The installer accepts `[string[]]$ExtensionId`, validates each ID, writes all IDs to the `allowed_origins` array, and stores the semicolon-separated allow-list in `HKCU\Software\Scrobblix\NativeMessaging` as `AllowedChromeExtensionIds`. Chrome and Edge can issue different IDs for the same unpacked source; register both actual IDs rather than assuming they match.
- **Firefox:** `browser-extension/manifest.firefox.json` is the separate Manifest V3 development manifest with Gecko add-on ID `scrobblix@frpboy.in`. Firefox uses `allowed_extensions` and registers under `HKCU\Software\Mozilla\NativeMessagingHosts\com.frpboy.scrobblix`. The allow-list, manifest and running add-on identity must match. Signed distribution requires the stable ID of that signed package.

Example registration for different Chrome and Edge IDs:

```powershell
.\scripts\install-native-host.ps1 -RegisterChrome -RegisterEdge -ExtensionId @("CHROME_EXTENSION_ID", "EDGE_EXTENSION_ID") -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host"
```

Firefox-only development registration:

```powershell
.\scripts\install-native-host.ps1 -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host" -RegisterFirefox -FirefoxExtensionId "scrobblix@frpboy.in"
```

The installer copies files into `%LOCALAPPDATA%\Scrobblix\NativeHost` and writes HKCU values; it does not require a machine-wide registry entry. The uninstaller removes browser registration/trust entries and attempts removal of the dedicated per-user host folder without deleting listening history or logs.

## Validation in code and CI

The code and CI checks verify:
- Chromium MV3 uses `background.service_worker`, not legacy `background.scripts`.
- The browser service worker rejects unsupported origins and malformed/out-of-bounds metadata payloads.
- Native host sender identity is checked before any response or dispatch, including `ping.v1`.
- An empty or mismatched extension allow-list fails closed.
- The installer can allow-list multiple Chromium IDs; the uninstaller includes Firefox registration and per-user file cleanup.
- Unit/assertion tests include multiple-ID cases, and a Node test exercises sender-origin/payload validation.

These are automated source/build checks. They are not a substitute for actual installed Chrome, Edge and Firefox tests, nor do they certify distribution security.

## Desktop pipe peer check and residual risk

The desktop pipe is current-user-only and performs additional client-image/parent-process checks. These checks are defense-in-depth, **not cryptographic process identity**. They do not replace signed binaries, controlled installation-directory ACLs, stronger publisher/process identity checks, race/handle review or a formal threat assessment. A same-user attacker may be able to tamper with per-user files or registrations; do not claim production-grade local IPC authentication on the basis of path/parent-name checks.

## Remaining qualification

- Install and test Chrome, Edge and Firefox separately on Windows, using actual registered IDs.
- Verify real playback event delivery and local History update for track change, play/pause/seek/end.
- Verify browser restart, extension reload, host unavailable/restart, desktop restart and reconnect behavior.
- Verify wrong/forged sender identity, unsupported origin, malformed/oversized frames, invalid JSON, unsupported protocol and empty allow-list are rejected.
- Verify the uninstall script removes registrations and its dedicated host folder; inspect residual files if a running process locks them.
- Qualify host update/rollback, signed host distribution, installation-directory ACLs and the same-user process-identity threat.

Results must be recorded in the [Real-world Manual Test Matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md). The live browser tests remain `NOT RUN`.


## Host-not-found diagnosis and recovery

The browser popup's **Specified native messaging host not found** error is a host-resolution failure reported by the browser. At this boundary, the browser has not demonstrated that it launched `Scrobblix.NativeHost.exe`; do not diagnose this as an executable crash or a protocol failure without separate evidence.

Connection flow:

```text
Popup Test connection
  -> runtime.sendMessage to extension service worker
  -> runtime.connectNative("com.frpboy.scrobblix")
  -> browser-specific per-user registration
  -> native-host manifest (name/type/path/allowed identity)
  -> Scrobblix.NativeHost.exe
  -> ping.v1 / pong.v1 (protocolVersion + matching messageId)
  -> popup shows verified connection
```

The extension cannot directly inspect arbitrary Windows registry state. It can classify the browser's native-messaging error and verify a valid host response; manifest/executable path details must be checked by the setup/repair script or by the user on Windows.

### Popup diagnostic categories

- `HOST_NOT_REGISTERED`: the browser reports that the named host cannot be resolved. Check the current browser's HKCU registration, its default value, and the manifest at that exact path. Repair only the affected browser. Restart that browser after repair.
- `EXTENSION_NOT_AUTHORIZED`: the host responds with the trusted-host authorization error or the browser denies the extension/policy. Register the exact extension ID shown in that browser; Chrome and Edge IDs can differ. Restart the browser after changing registration.
- `HOST_STARTUP_FAILED`: registration appears to be resolved but the host exits before the handshake. Validate the installed executable and its dependencies, then repair and retry.
- `PROTOCOL_ERROR`: the host responds, but not with a valid protocol-v1 `pong.v1` for the current message ID. Update the host and extension from the same build.
- `CONNECTION_TIMEOUT`: no valid handshake arrives within three seconds. Check stale processes, files and endpoint protection before reinstalling.
- `UNKNOWN_CONNECTION_ERROR`: the evidence does not support a more precise category. Use the diagnostic ID in a support report.
- `CONNECTION_SUCCESS`: success is emitted only after a `pong.v1` response matches protocol version 1 and the request's message ID.

The popup shows a bounded recovery hint and a support diagnostic ID. It must not expose stack traces, credentials or unrelated system details.

### Safe validation on Windows

Run the setup helper from the Scrobblix source tree or the shipped package as appropriate. It operates on per-user HKCU registration; do not elevate it or create machine-wide keys as a generic workaround.

For Chromium, use the actual browser extension ID:
```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Chrome -ExtensionId "ACTUAL_CHROME_EXTENSION_ID"
```

For Edge, replace `Chrome` with `Edge` and use the actual Edge ID. To configure both, pass both actual IDs to `-Browser ChromeAndEdge`. Firefox is a separate registration and uses the add-on ID / `allowed_extensions` contract:
```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Firefox -FirefoxExtensionId "scrobblix@frpboy.in"
```

The installer checks the generated manifest's JSON, host name, stdio type, executable path and configured extension allow-list, and verifies the selected registry registration points to the expected manifest. These checks validate local files/registration only; press Test connection afterward to test a real browser handshake.

If the package does not exist, build it first with ` .\scripts\setup-browser-companion.ps1 -Browser BuildOnly `. Building can replace generated package artifacts; it does not register a browser host. Do not run uninstallation merely to troubleshoot: it removes registrations for all supported browsers.

### Validation limits

Static validation and mocks do not qualify installed browsers. Live Chrome, Edge and Firefox tests, exact extension IDs, registry-resolution behavior and real playback forwarding must be recorded separately in the Real-world Manual Test Matrix.
