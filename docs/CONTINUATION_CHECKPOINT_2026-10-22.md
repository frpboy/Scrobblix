# Scrobblix continuation checkpoint — 2026-10-22

This checkpoint exists so implementation can resume from the last committed state instead of restarting or replacing existing work.

## Repository state at checkpoint

- Repository: https://github.com/frpboy/Scrobblix
- Protected integration target: `main` (do not force-push/reset or make experimental direct changes).
- Active continuation branch: `feat/scrobblix-v1-complete`
- PR #4 (v1 baseline): merged — https://github.com/frpboy/Scrobblix/pull/4
- PR #5 (local reliability/browser packaging/release-gate docs): open as a draft — https://github.com/frpboy/Scrobblix/pull/5
- PR #5 head at this checkpoint: `72e88397ff13c7b8d5c106d0e1e7b92718c0a65e`
- Reported green Windows CI for the prior PR #5 head: https://github.com/frpboy/Scrobblix/actions/runs/39722340271 (commit `44af3effca4ebcf23fe6dfdb427ce5bbf3e5aa51`). Re-check CI against the newest head before relying on it; the checkpoint commit itself has not been validated by that older run.
- PR #5 contains the browser companion packaging helper, Chromium MV3 manifest correction, PowerShell parser validation, local build-lock handling, browser bundle path validation, troubleshooting docs and a master implementation plan.

## Do not redo completed work

The current baseline already includes the WinUI desktop application, GSMTC playback observation, canonical tracking and source arbitration, confirmed-listening eligibility, SQLite history and durable idempotent queue, Last.fm scrobble/Now Playing/Love/Unlove flows, Credential Locker integration, MusicBrainz/Cover Art Archive/Last.fm metadata fallback, local artwork cache, diagnostics, backup/restore, tray and notification prototypes, Chromium/Firefox development companion support, and Windows CI/test bundles.

Architecture constraints remain: desktop GSMTC must work without the browser extension; the extension is optional; no Shazam/audio fingerprinting; do not claim production readiness from CI alone.

## Next implementation order

1. **Reconcile PR #5:** inspect the latest PR checks/review findings, run CI on the actual head, and keep it draft until code/docs and checks are reviewed. Do not merge automatically.
2. **Correctness and delivery:** exercise real Last.fm authorization and submissions, reconcile accepted actions against the account's scrobble history, and test repeated-track/restart/network-retry behavior for duplicate prevention.
3. **Browser integration:** install and test Chrome and Edge separately with their real extension IDs; validate Native Messaging event delivery, disconnect/reconnect and uninstall. Test Firefox temporary add-on behavior separately.
4. **Windows lifecycle:** test minimize/restore/Exit, Explorer restart tray recovery, notification activation/privacy, duplicate-instance prevention, high-DPI/high-contrast and clean shutdown on a real Windows machine.
5. **Security/release:** review same-user IPC identity limits, secret/log/support-bundle redaction, backup migration recovery, retention/delete controls, signed MSIX/App Installer, update/rollback/uninstall, clean-machine prerequisites, dependency licenses and SBOM.
6. **Quality:** add/execute long-run and accessibility coverage for pause/seek/ad transitions, late/missing metadata, provider throttling/revoked sessions, time-zone boundaries, queue recovery and larger history exports.
7. **Stitch UI design:** use Stitch MCP to create/edit the real Scrobblix design workspace only if the connector is available. The checked-in `Scrobblix-Docs/04_UserExperience/STITCH_IMPORT_BRIEF.md` is a handoff, not proof that Stitch was updated. Map approved editable frames to the existing WinUI views; do not replace the real app with a mockup.

## Resume commands

In the existing Windows clone, inspect local changes first and close Scrobblix before rebuilding:

```powershell
git status --short
git fetch origin --prune
git switch feat/scrobblix-v1-complete
git pull --ff-only origin feat/scrobblix-v1-complete
.\scripts\update-and-run.ps1
```

For validation without launching the app:

```powershell
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --no-restore --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release --no-build
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
```

If work is interrupted, first read this checkpoint and `docs/IMPLEMENTATION_RESUME.md`, inspect the active PR and latest CI SHA, then continue from the highest-priority unfinished item. Never assume the PR branch head or CI status stayed unchanged.

## Tooling honesty

The available tools in the current session did not expose a Stitch MCP integration. Therefore no claim is made that an external Stitch project was created or edited in this session. Continue the UI work using the saved brief until Stitch is actually connected, then verify the workspace and save the editable frames.
