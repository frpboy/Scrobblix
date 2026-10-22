# Native Messaging Host

> **Status:** Development implementation with automated checks; real-browser qualification and formal security review remain open
> **Updated:** 2026-10-22

## Executable role and framing

\`Scrobblix.NativeHost.exe\` reads length-prefixed JSON from stdin, caps frames at 256 KiB, validates the protocol version/message envelope and forwards supported playback-observation messages to the desktop app's current-user named pipe. Native-messaging stdout is reserved for protocol responses; diagnostic logging must not be written to stdout. The host does not contain Last.fm credentials.

## Browser identity and per-user registration

- **Chrome/Edge:** The native manifest uses \`allowed_origins\` with every explicitly supplied extension origin. The installer accepts \`[string[]]$ExtensionId\`, validates each ID, writes all IDs to the \`allowed_origins\` array, and stores the semicolon-separated allow-list in \`HKCU\Software\Scrobblix\NativeMessaging\` as \`AllowedChromeExtensionIds\`. Chrome and Edge can issue different IDs for the same unpacked source; register both actual IDs rather than assuming they match.
- **Firefox:** \`browser-extension/manifest.firefox.json\` is the separate Manifest V3 development manifest with Gecko add-on ID \`scrobblix@frpboy.in\`. Firefox uses \`allowed_extensions\` and registers under \`HKCU\Software\Mozilla\NativeMessagingHosts\com.frpboy.scrobblix\`. The allow-list, manifest and running add-on identity must match. Signed distribution requires the stable ID of that signed package.

Example registration for different Chrome and Edge IDs:

\`\`\`powershell
.\scripts\install-native-host.ps1 -RegisterChrome -RegisterEdge -ExtensionId @("CHROME_EXTENSION_ID", "EDGE_EXTENSION_ID") -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host"
\`\`\`

Firefox-only development registration:

\`\`\`powershell
.\scripts\install-native-host.ps1 -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host" -RegisterFirefox -FirefoxExtensionId "scrobblix@frpboy.in"
\`\`\`

The installer copies files into \`%LOCALAPPDATA%\Scrobblix\NativeHost\` and writes HKCU values; it does not require a machine-wide registry entry. The uninstaller removes browser registration/trust entries and attempts removal of the dedicated per-user host folder without deleting listening history or logs.

## Validation in code and CI

The code and CI checks verify:
- Chromium MV3 uses \`background.service_worker\`, not legacy \`background.scripts\`.
- The browser service worker rejects unsupported origins and malformed/out-of-bounds metadata payloads.
- Native host sender identity is checked before any response or dispatch, including \`ping.v1\`.
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

Results must be recorded in the [Real-world Manual Test Matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md). The live browser tests remain \`NOT RUN\`.
