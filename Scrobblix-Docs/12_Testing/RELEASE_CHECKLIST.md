# Release Checklist

> **Status:** Development baseline; production release gates remain open

## Automated validation

The Windows CI workflow runs on pull-request commits. It restores/builds the .NET solution, runs the domain/SQLite assertions, installs and checks/builds the browser companion, validates Chromium and Firefox manifests/host hooks, publishes the unpackaged desktop executable and native host, and uploads the Windows test bundle. The latest successful integrated code baseline before this documentation refresh is [CI run 39717582856](https://github.com/frpboy/Scrobblix/actions/runs/39717582856). Always check the most recent run shown on [PR #4](https://github.com/frpboy/Scrobblix/pull/4) because later commits need their own green run.

## Must complete before a production release

- [ ] Real Windows UI smoke test: Home, History, Queue actions, Statistics, Loved, Settings, Diagnostics and local CSV export.
- [ ] Live Last.fm account authorization, disconnect/reconnect and real scrobble/Now Playing/Love/Unlove verification; confirm counters only count accepted scrobbles.
- [ ] Test notification registration, disabled-by-default behavior, accepted-scrobble wording, hidden track details by default, lock-screen behavior and notification activation with the window hidden and after full exit.
- [ ] Test tray minimize/restore/Exit, app data integrity during Exit, DPI/scaling, taskbar/Explorer restart, and tooltip/accessibility. Tray recovery after Explorer restart is still to be implemented.
- [ ] Install the companion in Chrome and Edge and the temporary Firefox add-on on Windows; verify the exact registered IDs, message delivery, denied sender/host paths, browser restart and uninstaller.
- [ ] Back up/restore on real user data, corrupt archive/schema mismatch behavior, safety snapshot restoration and migration between all supported schema versions.
- [ ] Run long-duration soak/restart/recovery tests, concurrency tests on real GSMTC/browser overlap, performance budgets and accessibility qualification.
- [ ] Security-review the per-user native-host installation, path/parent process check, same-user threat model, registry ACLs and signed host identity.
- [ ] Build and validate signed MSIX/App Installer/updater, clean install/upgrade/rollback/uninstall, signing identity, provenance/SBOM and dependency/license inventory.
- [ ] Finalize privacy/store text, supported Windows/browser matrix, release notes, support docs and known issues.

No item above is considered complete merely because CI compiles or builds the test bundle. The test bundle is a development artifact, not a signed production installer.
