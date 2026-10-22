# Scrobblix v1 — implementation and release status

Updated: 2026-10-22  
Repository: https://github.com/frpboy/Scrobblix  
Default branch: `main`  
Continuation branch: `feat/scrobblix-v1-complete`  
Open implementation PR: https://github.com/frpboy/Scrobblix/pull/5

## Verified checkpoint

The `main` branch head observed during this review was `ae5fadb4c42e0738d80e842dbb91bd76aa08da4f` (`fix: clarify connected Last.fm state`). No statuses were returned for that exact SHA by the status endpoint. Treat it as **not validated**, not green.

The latest observed PR #5 Actions run was [39726924128](https://github.com/frpboy/Scrobblix/actions/runs/39726924128) (2026-10-22). It failed at **Build .NET solution**, so every later test/build/package stage in that workflow was skipped. The log identifies the blocker precisely:

```
src/Scrobblix.Desktop/App.xaml.cs(24,22): error CA1001:
Type 'App' owns disposable field(s) '_singleInstanceActivationEvent',
'_singleInstanceListenerCancellation', '_singleInstanceSemaphore', '_trayIcon'
but is not disposable.
```

This happens after restore succeeded and several library/test projects compiled. It is a real build failure, not a Last.fm or browser-runtime failure. Fix lifetime/disposal ownership correctly instead of blindly disabling CA1001.

## Priority 0 — unblock and revalidate CI

- [ ] Resolve CA1001 in `src/Scrobblix.Desktop/App.xaml.cs`; make event, cancellation source, semaphore and tray cleanup deterministic across normal close, startup failure and duplicate-instance exit.
- [ ] Commit the fix to `feat/scrobblix-v1-complete`.
- [ ] Re-run the workflow on the exact newest PR head and verify the full job succeeds, including test assertions, browser type-check/build, manifest validation, PowerShell parsing, publish and artifact upload.
- [ ] Update this status file only with evidence tied to the actual SHA/run.

## Priority 0 — Last.fm, identity and queue correctness

- [ ] Use a dedicated Last.fm test application/account and prove a qualifying scrobble appears in the profile.
- [ ] Reconcile each submitted action with the provider result. Mark delivered only after provider acceptance; keep ambiguous failures retryable and explain their status.
- [ ] Exercise duplicate event delivery, identical metadata reported from two sources, repeat of the same song after a different track, pause/resume, seeking, late metadata, track duration changes, and restart mid-queue.
- [ ] Verify queue lease expiration, bounded retry/backoff, crash recovery and no silently discarded qualifying track.
- [ ] Run a duplicate-free soak test and keep redacted event/action correlation IDs as evidence. Never automate against production listening history.

## Priority 1 — browser integration

- [ ] Build the companion and load the unpacked Chromium extension in Chrome and Edge separately.
- [ ] Record the actual extension ID from each browser; register matching allowed IDs for the native host and desktop policy.
- [ ] Restart browsers and desktop app, play/pause/seek/skip in supported web players, close/reopen tabs, suspend/resume, and verify events reach local history and the canonical playback pipeline.
- [ ] Load the Firefox development add-on through `about:debugging`, register the matching add-on identity, and verify equivalent event/reconnect behavior.
- [ ] Validate disconnect/reconnect snapshots, malformed/oversized messages, native-host stdout discipline, allow-list rejection and uninstall cleanup.
- [ ] Do not describe a temporary Firefox add-on as a persistent signed release.

## Priority 1 — Windows qualification and release

- [ ] Test tray restore, Open/Exit, minimize/hide and icon recovery after Windows Explorer restarts.
- [ ] Test notification opt-in, activation, persistence and track-detail privacy; notifications remain off by default and metadata stays hidden unless enabled.
- [ ] Test keyboard navigation, Narrator, focus order, contrast, DPI/scaling and window resize on real Windows hardware/session.
- [ ] Review per-user named-pipe access, process-identity assumptions, single-instance synchronization handles and malformed/rate-limited inbound messages.
- [ ] Test clean install, upgrade, failed update recovery, rollback, uninstall and retained-data choices.
- [ ] Produce a signed MSIX/App Installer or complete Store distribution identity. Verify signature and timestamp, publisher identity, update path, dependency/license review and SBOM before release.
- [ ] Test backup and restore across supported SQLite schema versions, including corrupt archive, checksum mismatch and interrupted restore.

## Priority 2 — UI / Stitch completion

The handoff brief is stored in `Scrobblix-Docs/04_UserExperience/STITCH_IMPORT_BRIEF.md`. The Stitch MCP integration was unavailable during this session. A Git brief is not evidence that the remote Stitch workspace changed.

- [ ] Import the brief into the real Stitch workspace when its integration is available.
- [ ] Generate/review Home, History, Queue, Sources, Diagnostics and Settings screens against the existing WinUI controls and design tokens.
- [ ] Validate loading, empty, error, connected, queued and provider-rejected states; keyboard/focus, contrast, screen scaling and reduced-motion requirements.
- [ ] Commit exported assets/specs and add a workspace/project link after the external changes have actually happened.

## Evidence rules

A clean build proves compilability for that commit and runner only. A passing unit/assertion suite does not prove live Last.fm acceptance, real browser event delivery, GUI tray recovery, accessibility qualification, signing, upgrade or rollback. Mark those gates complete only when their specific environment was tested and the evidence is recorded.

## Resume / validation commands

Run in Windows PowerShell with the desktop app closed:

```powershell
git fetch origin --prune
git switch feat/scrobblix-v1-complete
git pull --ff-only origin feat/scrobblix-v1-complete
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --no-restore --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release --no-build
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
```

Do not put Last.fm API secrets or session keys in source control, logs, screenshots or issue comments.


## P0 issue #6 — App disposable-resource ownership (2026-10-22)

- The current `main` implementation in `src/Scrobblix.Desktop/App.xaml.cs` already includes the CA1001 fix: `App` implements `IDisposable`, owns the single-instance semaphore, activation event, listener cancellation source and tray icon, and disposes them on duplicate-instance, window-construction failure and normal close paths. Normal close and explicit disposal share the listener cleanup method.
- This candidate adds durable source-contract assertions for those paths and manual matrix case `P0-OWN-01`. The physical Windows duplicate-start/close/relaunch scenario is still `NOT RUN`.
- The first current-main CI attempt built successfully but failed at the domain assertion because the migration test expected four rows while `SqliteDatabase` embeds five migrations (`0001`–`0005`). The test expectation is corrected to five in this candidate. This is a test-count correction, not a database migration change.
- Candidate commit and matching full Windows workflow: pending. Keep issue #6 open until that exact-head workflow succeeds.
