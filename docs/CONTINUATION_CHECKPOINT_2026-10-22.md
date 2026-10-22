# Scrobblix continuation checkpoint — 2026-10-22

This checkpoint is a source-of-truth handoff for continuing from the current committed state. Inspect live GitHub state again before relying on any SHA or CI result.

## Repository state

- Repository: https://github.com/frpboy/Scrobblix
- Protected integration target: `main`; do not force-push/reset or make experimental direct changes.
- Current main SHA at checkpoint: `0436ed83762971ef75dae5be1ffd15bab7e1eae5`.
- Post-merge CI for that SHA: [run 39812019297](https://github.com/frpboy/Scrobblix/actions/runs/39812019297), **IN PROGRESS when this checkpoint was written**.
- PR #28 (SQLite restore sidecar rollback): merged to main. Exact-head Windows CI passed at [run 39785201272](https://github.com/frpboy/Scrobblix/actions/runs/39785201272).
- PR #26 was a duplicate restore candidate and has been closed as superseded by #28.
- PR #25 privacy fix, #23 migration-history safety, #21 preview-release automation, and #19 compact hamburger navigation are merged.
- Preview release [v0.1.0-beta.1](https://github.com/frpboy/Scrobblix/releases/tag/v0.1.0-beta.1) exists. It is an unsigned preview/test bundle, not a signed installer or stable release.
- Active release qualification issue: [#17](https://github.com/frpboy/Scrobblix/issues/17).
- Documentation correction issue: [#27](https://github.com/frpboy/Scrobblix/issues/27). This checkpoint refresh is part of the fix; close the issue only after the documentation PR is merged and the current-main evidence is rechecked.

## Do not redo completed work

The baseline includes the WinUI desktop app, GSMTC playback observation, canonical tracking/source arbitration, confirmed-listening eligibility, SQLite history and durable queue, Last.fm scrobble/Now Playing/Love/Unlove flows, Credential Locker integration, MusicBrainz/Cover Art Archive/Last.fm metadata fallback, local artwork cache, diagnostics, backup/restore, tray/notification prototypes, Chromium/Firefox companion support, and Windows CI/test bundles.

Already merged fixes include:
- App resource ownership and lifecycle cleanup (PR #9).
- Listening-metadata redaction and follow-up delimiter-safe redaction (PRs #11 and #25).
- Loved-track/artwork fallback UI (PR #12).
- Local-day/DST statistics boundaries (PR #16).
- Explicit browser-origin authorization (PR #18).
- Hamburger sidebar and clickable compact icon rail with destination tooltips (PR #19).
- Validated preview-release automation (PR #21).
- Unknown/inconsistent migration-history rejection (PR #23).
- Fail-closed SQLite restore swap with WAL/SHM rollback set, recoverable rollback artifacts, and Windows regression tests (PR #28).

Architecture constraints:
- Desktop GSMTC must work without the browser extension; the extension is optional.
- Do not add Shazam/audio fingerprinting.
- Do not claim production readiness from CI alone.
- Never expose Last.fm secrets/session keys or raw listening metadata in logs.

## Next implementation order

1. **Verify current main CI.** Recheck [run 39812019297](https://github.com/frpboy/Scrobblix/actions/runs/39812019297) against exact SHA `0436ed83762971ef75dae5be1ffd15bab7e1eae5`. Update release status only after it completes.
2. **Execute release qualification issue #17.** Use a dedicated Last.fm test account; reconcile accepted submissions; test duplicate/restart/network-retry behavior, long-running playback, browser native messaging, and Windows lifecycle/accessibility.
3. **Validate installation and distribution.** Test clean install, upgrade, failed update recovery, rollback/uninstall, retained-data behavior, signing/publisher identity, dependency licensing, SBOM, and artifact provenance.
4. **Record evidence, not assumptions.** Keep unexecuted environment-dependent cases NOT RUN/BLOCKED. Include exact candidate SHA, environment, outcome, and sanitized evidence.
5. **Stitch UI design.** The handoff is `Scrobblix-Docs/04_UserExperience/STITCH_IMPORT_BRIEF.md`. No claim of external Stitch workspace modification is valid until the integration is available and the actual workspace is inspected.

## Validation commands

In Windows PowerShell with the desktop app closed, inspect local changes before rebuilding:

```powershell
git status --short
git fetch origin --prune
git switch main
git pull --ff-only origin main
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --no-restore --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release --no-build
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
```

## Tooling and evidence honesty

The exact-head Windows CI for PR #28 passed. The post-merge main workflow was still in progress at this checkpoint. Real Last.fm acceptance, installed Chrome/Edge/Firefox behavior, GUI tray recovery, accessibility, signed distribution, and install/upgrade/rollback are separate qualification gates. Do not mark any of those PASS unless they were actually executed.
