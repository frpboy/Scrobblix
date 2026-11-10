# Getting Started - Scrobblix Preview

> **Audience:** Scrobblix preview users and testers  
> **Status:** User setup guide; this is not a promise of a signed production installer  
> **Last reviewed:** 2026-11-10

## Before you begin

The current Windows beta.9 download is a single unsigned Setup.exe with bundled private runtimes, not a signed MSIX/App Installer or stable production release. Use the [published release](https://github.com/frpboy/Scrobblix/releases/tag/windows-v0.1.0-beta.9), verify Setup.exe against that release's SHA256SUMS.txt, and read the [Windows installer guide](../../docs/WINDOWS_INSTALLER.md). Do not bypass Windows security protections.

Scrobblix targets Windows 11. The current release target is Windows 11 25H2 or later, but real-device qualification remains pending. Local history and tracking can operate without a Last.fm connection. Remote scrobbling requires browser authorization; the default connection does not require users to supply developer API keys.

## First run

1. Exit any older Scrobblix copy from its system tray, run beta.9 Setup.exe, then open Scrobblix from Start. Keep the old extracted ZIP folder until checking the installed app; setup leaves it intact and reuses the existing local profile.
2. Open Scrobblix and confirm its startup status indicates that local storage initialized.
3. Open Settings. The default Last.fm connection uses the public authorization broker. Existing user-owned developer credentials remain an optional advanced direct route; they are not required for normal connection.
4. Choose **Connect Last.fm**, approve Scrobblix in the browser, then return to the app and finish authorization. Never enter your Last.fm password into Scrobblix.
5. Start a supported desktop player and play a track with valid artist/title/duration metadata.
6. Confirm that the current track appears on Home and its confirmed-played-time progress advances only while playback is observed as playing.
7. If you want browser-specific site metadata, install the optional Browser Companion using the instructions in the included bundle or repository. Its unpacked extension ID must be registered exactly for each browser; a desktop GSMTC source may work without it.
8. Observe the eligibility threshold and Queue. A qualifying play should create a durable action, but only a provider-accepted result confirms a remote scrobble.

## What healthy behavior means

- The app has local storage and shows a status appropriate to the active source.
- Current media metadata is visible when Windows/player exposes it.
- The eligibility threshold advances only from confirmed playing time.
- A qualifying play creates one durable provider action.
- The queue provides an explicit accepted, ignored, retry/auth, permanent-failure or ambiguous-outcome state.
- Last.fm remote history is checked separately when verifying actual acceptance.

## Privacy and data handling

History, logs and cached artwork are stored locally. The Browser Companion sends observations to the local desktop app and does not directly call Last.fm or store Last.fm credentials. Notifications are off by default; exposing artist/title in notifications requires a separate setting. Review the support-bundle exclusions before sharing diagnostics.

## Where to go next

- [Daily use](DAILY_USE.md) for current app behavior.
- [Troubleshooting](TROUBLESHOOTING.md) for source/queue/authorization problems.
- [Last.fm connection](CONNECT_LASTFM.md) for the real authorization sequence.
- [Installation](INSTALLATION.md) for the current preview/install limitations.
- [Real-world manual test matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md) for qualification status.
