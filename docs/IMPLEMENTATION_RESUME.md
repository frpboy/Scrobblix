# Scrobblix implementation resume guide

> **Repository:** `frpboy/Scrobblix`  
> **Working branch:** `feat/scrobblix-v1-complete`  
> **Review:** [PR #4](https://github.com/frpboy/Scrobblix/pull/4)  
> **Last green integrated code baseline before this status-document refresh:** [CI run 39717582856](https://github.com/frpboy/Scrobblix/actions/runs/39717582856), commit `f6dffd5cf95e6266c4b7aa58cdf4576077dbd6ab`

## What is currently present

- Live desktop foundation seen in the owner's screenshots: GSMTC playback, source identity, confirmed-listening eligibility, local history, Today's persisted totals, queue status and Last.fm account state.
- Durable queue with idempotency, leasing/retry/recovery and provider-accepted state; Last.fm scrobble/Now Playing/Love/Unlove provider action paths.
- Late-metadata-tolerant tracking, cross-source arbitration, local artwork and metadata fallback, numbered local logs and UI error states.
- SQLite consistent backup, fixed-format ZIP validation, SHA-256/schema/integrity validation, staged restore and a pre-restore safety snapshot.
- Minimize-to-tray prototype with restore and explicit Exit. Tray icon recovery after Explorer restart is not yet implemented.
- Opt-in Windows App SDK notifications; notification text hides artist/title by default and requires a separate explicit setting to reveal track details. Queue worker outcomes are used so only an accepted scrobble creates a success notification.
- Separate Firefox Manifest V3 development manifest, add-on-ID allow-list/installer path, Chromium/Firefox static-manifest validation, and desktop-side native-host client-image/parent-browser-name checks.
- Windows CI builds and runs .NET assertions, checks/builds the TypeScript browser companion, publishes the desktop/native host and assembles a test bundle.

## Continue from the last local implementation

Open PowerShell in your existing repository clone and make sure there are no local changes you need to keep:

```powershell
git status --short
git fetch origin --prune
git switch feat/scrobblix-v1-complete
git pull --ff-only origin feat/scrobblix-v1-complete
.\scripts\update-and-run.ps1
```

If you want a manual development build instead of the helper:

```powershell
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
dotnet run --project src/Scrobblix.Desktop/Scrobblix.Desktop.csproj --configuration Release
```

The branch is intentionally separate from `main`; do not force-push or reset `main`. Push any further work to this branch and use PR #4 for review. Latest branch HEAD and latest CI result may have moved beyond the baseline SHA above; use GitHub to verify before distributing an artifact.

## Still requires real-machine or production credentials

- Validate tray behavior and notifications interactively in Windows, including Explorer restart, lock-screen privacy and notification activation after exit.
- Test the Firefox companion in a real Firefox installation, and Chrome/Edge with actual extension IDs and native-host registration.
- Verify Last.fm authorization and each provider action against the real account/API, not only mock assertion handlers.
- Complete installer signing/MSIX/App Installer/updater, rollback/upgrade/uninstall and clean-machine runtime tests.
- Complete accessibility, performance/long-run, privacy/security and release checklist reviews. The current process-path/parent-name check is defence-in-depth, not cryptographic publisher identity.
- Stitch MCP was not available in the authoring session. The checked-in Stitch brief is a reusable handoff; it is not evidence that the external Stitch workspace has been updated.
