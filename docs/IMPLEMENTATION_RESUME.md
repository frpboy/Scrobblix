# Scrobblix implementation resume guide

> **Repository:** `frpboy/Scrobblix`  
> **Integration baseline:** `main`  
> **Read first:** [Private Beta Readiness](PRIVATE_BETA_READINESS.md) and [Real-world manual test matrix](../Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md)

## Current position

The codebase already includes the principal everyday surfaces—Home, History, Statistics, Loved, Queue, Sources, Diagnostics and Settings—plus GSMTC tracking, Last.fm action/queue paths, local backup/restore, tray/notification code and browser companion manifests/host scripts.

The current phase is proving that this implementation is correct and installable over time. Uploaded logs show a large burst of `All pipe instances are busy` Browser Companion warnings and a separate local rebuild colliding with a live `Scrobblix.Desktop` process. Targeted fixes for retry/log amplification and the branch/lock guard are in this continuation. The logs do not contain a fatal exception that conclusively proves why the application exits, so retest and capture Windows exit evidence before closing the crash investigation. Do not re-plan Loved, Statistics, basic tray behavior, notifications, backup/restore, or manifest existence as missing product features; plan their **verification and hardening** instead. The Diagnostics-only support-bundle ZIP is now implemented on this continuation branch with a strict field allow-list and explicit confirmation. Canary-redaction CI plus Windows Save/export/accessibility qualification remain open. Source-health clarity, onboarding and signed install/update/rollback are also release tasks.

## Current-main verification discipline

The latest `main` head verified during this pass was `1aab53b6e3871e4d53c6f27b3a496087a8cc29ae`, with the matching Windows CI workflow [run 39730929297](https://github.com/frpboy/Scrobblix/actions/runs/39730929297) completing successfully on that exact SHA. PR #7 (`feat/private-beta-readiness`) remains an unmerged draft; validate its own current head and matching run before relying on any continuation-branch change. Always fetch current `main` and its latest relevant Actions run rather than relying on historical statuses.

## Merge a feature branch and rebuild main

The `scripts/update-and-run.ps1` helper integrates the remote feature branch (or `-Branch <name>`) into the local feature branch, switches to `main`, integrates remote `main`, merges the feature branch with a merge commit, then rebuilds. It refuses dirty worktrees and aborts an unresolved merge rather than leaving partial Git state. It also refuses to build while any `Scrobblix.Desktop` process is running; close the main window and minimized-to-tray instance first.

```powershell
git status --short
.\scripts\update-and-run.ps1
```

## Continue from main

In Windows PowerShell, from your existing clone, first commit/stash local edits you need to keep and close the app before rebuilding (loaded assemblies can cause MSBuild file-lock errors):

```powershell
git status --short
git fetch origin --prune
git switch main
git pull --ff-only origin main
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
dotnet run --project src/Scrobblix.Desktop/Scrobblix.Desktop.csproj --configuration Release
```

The browser companion is optional for Spotify desktop when GSMTC yields sufficient metadata. Do not require browser install for every desktop Spotify acceptance case, but test browser integration before advertising the companion as qualified.

## Required results

After the pipe-backoff change, run CR-01 through CR-04 in the manual matrix. Backoff mitigates retry pressure and log growth; it is not proof that every unexpected exit is fixed.

Record outcomes in `Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md`. Keep result fields as Not run until executed on the specified Windows/browser/Last.fm environment. Attach only sanitized evidence. Never commit credentials, session keys, raw listening database or artwork cache.

## External release dependencies

- Spotify playback and Windows shell require a real Windows session.
- Live Last.fm delivery requires a dedicated test account and app credentials entered locally.
- Browser event verification requires actual installed Chrome/Edge/Firefox and their real extension IDs/add-on identity.
- Public signed distribution requires a publisher identity/certificate or a Microsoft Store identity.
- The current `main` design handoff now records a real Stitch workspace revision: [Stitch UI handoff](STITCH_UI_HANDOFF_2026-10-22.md). The draft PR merges that latest mainline Home copy into the delivery-status and Sources changes. Remaining UI work is Windows visual/responsive/accessibility qualification; the Stitch HTML and screenshot exports are references, not shipped assets.
