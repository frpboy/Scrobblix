# Scrobblix checkpoint and release gate status — 2026-10-22

## Verified repository state

- Repository: https://github.com/frpboy/Scrobblix
- Default branch: `main`
- Current main head observed during this review: `ae5fadb4c42e0738d80e842dbb91bd76aa08da4f` (`fix: clarify connected Last.fm state`)
- Continuation branch: `feat/scrobblix-v1-complete`
- Open follow-up: [PR #5](https://github.com/frpboy/Scrobblix/pull/5)
- Main is not validated green merely because an earlier commit passed.
- Latest observed PR #5 check: [Actions run 39726924128](https://github.com/frpboy/Scrobblix/actions/runs/39726924128), failed during `Build .NET solution`.

## Current confirmed CI blocker

Windows CI restores successfully, then fails building `src/Scrobblix.Desktop/App.xaml.cs`:

```
error CA1001: Type 'App' owns disposable field(s)
'_singleInstanceActivationEvent', '_singleInstanceListenerCancellation',
'_singleInstanceSemaphore', '_trayIcon' but is not disposable.
```

Because the build fails, assertion tests, browser type-check/build, manifest validation, PowerShell validation, publishing, and artifacts are skipped in that run. Fix ownership/disposal correctly, then rerun the complete workflow on the newest PR head.

## Ordered acceptance gates

### P0 — build and automated behavior
- [ ] Fix CA1001 by making App lifecycle cleanup explicit and correct (do not suppress the analyzer without a reviewed lifecycle argument).
- [ ] Get a fresh green Windows CI run on the exact newest commit.
- [ ] Add/verify deterministic tests for qualifying Last.fm submission, provider rejection, duplicate event/idempotency, same-track pause/resume, track repeats, seeking, overlapping GSMTC/browser sources, queue leasing and crash/restart recovery.
- [ ] Ensure provider-accepted state is only written after Last.fm accepted the action, and ambiguous network failures retry idempotently without silently dropping a play.
- [ ] Inspect build and assertion results before considering the CI gate passed.

### P1 — real environment integrations
- [ ] Last.fm: use a dedicated test account and test application; verify auth/session creation, now-playing, an accepted scrobble visible in the account, rejected action behavior, offline recovery, and duplicate-free repeated-track soak.
- [ ] Chrome and Edge: install the unpacked generated extension separately, record each real extension ID, register both IDs as needed, restart browser/app, then verify events reach desktop ingress and create local history.
- [ ] Firefox: load temporary add-on, register the matching add-on identity, restart, verify events and reconnect/snapshot behavior. Do not claim persistent distribution until signing/store identity is resolved.
- [ ] Exercise disconnect/reconnect, sleep/resume, lock/unlock, browser tab changes, seeking, missing metadata and overlapping GSMTC/browser reports.

### P1 — Windows desktop release
- [ ] Explorer restart restores tray icon; restore/open/exit paths work after minimize and notification activation.
- [ ] Notification opt-in and track-detail privacy persist; no details are displayed when disabled.
- [ ] Keyboard-only operation, Narrator, focus order, contrast, scaling and reduced-motion expectations are checked on a real Windows session.
- [ ] Review same-user named-pipe and single-instance event/handle security, process identity assumptions and malformed/oversized message behavior.
- [ ] Test backup/restore across supported schema versions, clean install, upgrade, rollback, uninstall and residual-data behavior.
- [ ] Prepare signed MSIX/App Installer or Store distribution; verify publisher identity, signature/timestamp, update path, dependency/SBOM and license review.

### P2 — Stitch design completion
- [ ] Import `Scrobblix-Docs/04_UserExperience/STITCH_IMPORT_BRIEF.md` into the actual Stitch workspace using the Stitch integration when available.
- [ ] Review generated Home, History, Queue, Sources, Diagnostics and Settings layouts against design tokens, accessibility and current WinUI controls.
- [ ] Record actual Stitch workspace/project link and exported artifacts in Git. A saved brief is not evidence of a Stitch workspace change.

## Evidence policy

A source-code assertion or passing CI build is not proof of actual Last.fm delivery, installed browser event delivery, tray recovery in Explorer, accessibility qualification, or signed installer upgrade/rollback. Only mark those gates after capturing test evidence for the relevant environment.

## Resume commands (Windows PowerShell)

```powershell
git fetch origin
git switch feat/scrobblix-v1-complete
git pull --ff-only origin feat/scrobblix-v1-complete
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
```

If the desktop app is running, close it before rebuild to avoid locked assemblies. Do not put Last.fm secrets in Git, logs, screenshots, or issue comments.
