# Scrobblix implementation resume guide

> **Repository:** `frpboy/Scrobblix`  
> **Integration baseline:** `main`  
> **Read first:** [Private Beta Readiness](PRIVATE_BETA_READINESS.md) and [Real-world manual test matrix](../Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md)

## Current position

The codebase already includes the principal everyday surfaces—Home, History, Statistics, Loved, Queue, Sources, Diagnostics and Settings—plus GSMTC tracking, Last.fm action/queue paths, local backup/restore, tray/notification code and browser companion manifests/host scripts.

The current phase is proving that this implementation is correct and installable over time. Do not re-plan Loved, Statistics, basic tray behavior, notifications, backup/restore, or manifest existence as missing product features; plan their **verification and hardening** instead. The Diagnostics-only support-bundle ZIP is now implemented on this continuation branch with a strict field allow-list and explicit confirmation. Canary-redaction CI plus Windows Save/export/accessibility qualification remain open. Source-health clarity, onboarding and signed install/update/rollback are also release tasks.

## Current-main verification discipline

The mainline snapshot checked during the 2026-10-22 review was `ae5fadb4c42e0738d80e842dbb91bd76aa08da4f`. The commit-status endpoint returned no individual statuses for that SHA in that review. Always fetch current `main` and the latest relevant Actions run rather than relying on that historical snapshot. A successful workflow on an unmerged continuation branch does not establish mainline CI status.

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

Record outcomes in `Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md`. Keep result fields as Not run until executed on the specified Windows/browser/Last.fm environment. Attach only sanitized evidence. Never commit credentials, session keys, raw listening database or artwork cache.

## External release dependencies

- Spotify playback and Windows shell require a real Windows session.
- Live Last.fm delivery requires a dedicated test account and app credentials entered locally.
- Browser event verification requires actual installed Chrome/Edge/Firefox and their real extension IDs/add-on identity.
- Public signed distribution requires a publisher identity/certificate or a Microsoft Store identity.
- Stitch MCP was unavailable during this review; an import brief is not evidence that a Stitch workspace changed.
