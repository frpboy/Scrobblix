# Scrobblix continuation checkpoint — 2026-10-23

This checkpoint is a source-of-truth handoff for continuing from the current committed state. Fetch live GitHub state before relying on an SHA or CI result.

## Repository state

- Repository: https://github.com/frpboy/Scrobblix
- Integration target: `main`; do not force-push/reset or make experimental direct changes.
- Current main SHA at checkpoint: `e0b99774bb62bcc2ab160a8e98b24051f382641b`.
- Exact post-merge Windows CI for this SHA: [run 39901957156](https://github.com/frpboy/Scrobblix/actions/runs/39901957156), **PASSED**.
- Preview release [v0.1.0-beta.1](https://github.com/frpboy/Scrobblix/releases/tag/v0.1.0-beta.1) exists. It is an unsigned development/test bundle, not a signed installer or stable release.
- Active release qualification issue: [#17](https://github.com/frpboy/Scrobblix/issues/17).
- Documentation reconciliation issue: [#27](https://github.com/frpboy/Scrobblix/issues/27). This branch refreshes the current audit report/register and release/continuation handoffs; close #27 only after the docs PR merges and its exact-head CI passes.

## Do not redo completed work

Current main includes the Windows desktop app, GSMTC playback observations, canonical source arbitration, confirmed-listening eligibility, SQLite history and durable queue, Last.fm scrobble/Now Playing/Love/Unlove flows, Credential Locker integration, MusicBrainz/Cover Art Archive/Last.fm metadata fallback, local artwork cache, diagnostics, backup/restore, tray/notification prototypes, Chromium/Firefox companion support and a Windows CI/test-bundle workflow.

Already merged fixes include:
- PR #9: app resource ownership and lifecycle cleanup.
- PRs #11 and #25: listening-metadata redaction.
- PR #12: loved-track/action and artwork fallback UI.
- PR #16: local-day/DST statistics boundaries.
- PR #18: explicit browser-origin authorization.
- PR #19: hamburger navigation and functional collapsed icon rail/tooltips.
- PR #21: validated preview-release workflow.
- PR #23: reject unknown/inconsistent migration histories.
- PR #28: fail-closed SQLite restore swap with WAL/SHM rollback.
- PR #31: preserve enriched artwork across repeated playback polls.
- PR #33: provider/account-scoped queue claims and DNS-pinned public artwork connections; issues #30/#32 closed.
- PR #35: CSV formula-prefix neutralization, including line-feed and full-width formula characters; issue #34 closed.
- PR #37: allow all persisted provider-action completion outcomes in the domain lifecycle helper; issue #36 closed.

Architecture constraints:
- Desktop GSMTC must work without the browser extension; the extension is optional.
- Do not add Shazam/audio fingerprinting.
- Do not claim production readiness from CI alone.
- Never expose Last.fm secrets/session keys or raw listening metadata in logs, commits, reports, or screenshots.

## Validation evidence

- Audit-start main SHA `0436ed83762971ef75dae5be1ffd15bab7e1eae5` passed Windows CI in [run 39812019297](https://github.com/frpboy/Scrobblix/actions/runs/39812019297).
- PR #33 exact-head Windows CI passed in [run 39895414307](https://github.com/frpboy/Scrobblix/actions/runs/39895414307).
- PR #35 exact-head Windows CI passed in [run 39896613043](https://github.com/frpboy/Scrobblix/actions/runs/39896613043).
- PR #37 exact-head Windows CI passed in [run 39901751712](https://github.com/frpboy/Scrobblix/actions/runs/39901751712).
- Current main `e0b99774bb62bcc2ab160a8e98b24051f382641b` passed the integrated Windows workflow in [run 39901957156](https://github.com/frpboy/Scrobblix/actions/runs/39901957156).
- Workflow includes .NET build, desktop UI and release-workflow contracts, domain/Infrastructure assertions, browser TypeScript/build/security/manifest checks, PowerShell parsing, Windows and NativeHost publishing, test-bundle assembly and artifact uploads.
- Local GitHub clone failed because this environment could not resolve `github.com`; local .NET/PowerShell executions are not claimed. The GitHub Actions results are the actual automated evidence.
- Branch-protection details were unavailable through the connected integration; no branch protection was changed.

## Remaining release qualification

[Issue #17](https://github.com/frpboy/Scrobblix/issues/17) remains open. The real-world manual matrix still records provider reconciliation, installed-browser/native messaging, long-run playback, GUI/tray/accessibility, backup/restore failure cases, and signed install/upgrade/rollback/uninstall as `NOT RUN`. Execute those only in an appropriate real Windows/browser environment and with a dedicated Last.fm test account; do not mark them PASS without evidence.

## Next implementation order

1. Review this documentation branch's diff, validate the JSON register and links, and open the focused documentation PR against `main`.
2. Wait for the exact documentation PR head's Windows CI to complete successfully; fix failures rather than bypassing checks.
3. Merge only if mergeability and required checks/review policy are satisfied; verify the merge SHA and post-merge CI, then close issue #27.
4. Keep issue #17 open until the real-world qualification matrix is executed and the release go/no-go is documented.

## Validation commands

In Windows PowerShell on a clean working tree with the desktop app closed:

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
node scripts/test-browser-security.mjs
node scripts/validate-browser-manifests.mjs
```

## Resume procedure

Fetch live `main`, open issues/PRs and latest workflow conclusions before continuing. Current reference point is main SHA `e0b99774bb62bcc2ab160a8e98b24051f382641b` and CI run 39901957156. Do not reuse those as proof for any later SHA.
