# Release Checklist

> **Status:** Private-beta readiness; release gates are open  
> **Updated:** 2026-10-22  
> **Integration baseline:** `main`

## CI — always bind evidence to a commit

Check the [current main branch](https://github.com/frpboy/Scrobblix/tree/main) and [Actions runs](https://github.com/frpboy/Scrobblix/actions). Record the exact `main` SHA and workflow run URL in the manual test report. A green run on a previous SHA or on an unmerged feature branch does not validate current `main`.

CI builds and tests the code paths included in that workflow. It does not certify live Last.fm acceptance, installed browser event delivery, GUI tray behavior, long-duration stability, accessibility or signed update/rollback.

## Current implementation vs. qualification

- Home, History/search/export, Statistics, Loved, Queue, Sources, Diagnostics and Settings: **surfaces exist in code; verify real interactions**.
- Last.fm auth/provider actions/queue, backup/restore path, tray behavior and opt-in privacy-aware notifications: **implementation exists; test with real provider/Windows and disposable test data**.
- Chromium/Firefox manifests and native-host scripts: **development integration exists; real installed browser delivery remains pending**.
- Explorer/taskbar restart recovery: **not established on the mainline code snapshot reviewed here**; test it and add recovery if absent.
- Support bundle: spec exists in operations docs; **export implementation and canary-redaction tests remain a release task**.
- Signed MSIX/App Installer and updater: **not delivered/qualified**.

## P0 — manual correctness and recovery

- [ ] Multi-day Spotify session scenarios completed and recorded in [Real-world manual test matrix](REAL_WORLD_MANUAL_TEST_MATRIX.md).
- [ ] Track-change detection, pause/resume/seek and confirmed played-time accounting make sense.
- [ ] Same track after an intervening track is scrobbled once per qualifying play, while duplicate source/replay events do not duplicate one play.
- [ ] Spotify advertisements/non-music are not scrobbled as music.
- [ ] Sleep/wake, offline/recovery, Last.fm outage/recovery and restart during pending/in-flight actions preserve queue correctness.
- [ ] Provider profile is reconciled with Scrobblix action IDs/outcomes from the test window.
- [ ] 8–24 hour soak: memory/CPU, DB size, logs, tray, restart and queue are stable and documented.
- [ ] Backup, restore, corrupt archive, schema mismatch and pre-restore safety snapshot verified on disposable non-production data.

## P1 — browser, security and support

- [ ] Chrome, Edge and temporary Firefox tested separately with actual IDs and event-delivery proof.
- [ ] Browser reload/restart, native host restart, disconnected port, malicious/forged IDs, malformed/oversized frames and uninstall path verified.
- [ ] Native host install is per-user; registry and manifest permissions, executable paths and extension allow-lists reviewed.
- [ ] Source health shows which playback source is observed/trusted and exposes recent contact/errors without claiming an installed browser that is not installed.
- [ ] Support ZIP has a preview and excludes secrets/session keys/history database/artwork; seeded canary values cannot appear in any archive file.
- [ ] Explorer restart, tray restore, notification activation/privacy/lock-screen and clean app shutdown tested on Windows.
- [ ] Keyboard-only, Narrator, focus, contrast and scaling checks passed.

## P1 — installable release

- [ ] Signed MSIX/App Installer or Store distribution path selected.
- [ ] Publisher identity, signature chain and timestamp verified.
- [ ] Clean install on another account/device.
- [ ] Upgrade from previous candidate, repair, failed-update recovery, rollback and uninstall.
- [ ] App data retention/deletion after uninstall is explicit and tested.
- [ ] Dependency/license inventory, SBOM, provenance, vulnerability review and release notes complete.
- [ ] Privacy notes, known issues, support instructions and supported environment matrix finalized.

Do not mark a gate complete from compilation alone. Store the run logs, sanitized screenshots or redacted correlation/action evidence with the test report; never upload secrets or full listening history.
