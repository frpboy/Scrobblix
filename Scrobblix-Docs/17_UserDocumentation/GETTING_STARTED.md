# Getting Started — Scrobblix Preview

> **Audience:** Scrobblix preview users and testers  
> **Status:** User setup guide; this is not a promise of a signed production installer  
> **Last reviewed:** 2026-10-28

## Before you begin

Current public artifacts are unsigned development preview ZIPs, not a signed MSIX/App Installer or stable production release. Read the repository's [release process](../../docs/RELEASE_PROCESS.md) and verify the checksum when supplied. The app may require the matching Windows App Runtime. Do not bypass Windows security warnings to run an unsigned public bundle.

Scrobblix targets Windows 11. The current release target is Windows 11 25H2 or later, but real-device qualification remains pending. Local history and tracking can operate without Last.fm credentials; remote scrobbling requires API credentials and an authorized Last.fm session.

## First run

1. Install/run the exact preview bundle according to that release's included test instructions.
2. Open Scrobblix and confirm its startup status indicates that local storage initialized.
3. In Settings, enter your Last.fm API key and shared secret and save them securely. The app stores them through Windows Credential Locker; development environment variables are an alternative initialization source.
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
