# Scrobblix v1 — implementation and release status

Updated: 2026-10-22  
Repository: https://github.com/frpboy/Scrobblix  
Default branch: `main`  
Continuation branch: `feat/private-beta-readiness`  
Open implementation PR: https://github.com/frpboy/Scrobblix/pull/5

## P0: App disposable-resource ownership

The prior observed workflow failed at `.NET solution build` with CA1001 because `App` owned an activation event, cancellation source, semaphore and tray icon without deterministic lifecycle ownership. The lifecycle fix has been carried into `feat/private-beta-readiness` without replacing its newer desktop UI work.

- `App` implements `IDisposable` and owns its single-instance `Semaphore`, activation `EventWaitHandle`, listener `CancellationTokenSource`, and `TrayIconService`.
- A duplicate launch signals the existing instance and disposes the new process's unused event/semaphore handles before exiting.
- If window construction fails after the single-instance gate is acquired, `Dispose()` releases the gate and other owned resources.
- Normal window close stops the activation listener and disposes owned resources. Cleanup is idempotent so explicit disposal and close don't double-release the gate.
- Source-contract assertions cover ownership and startup/close paths; manual matrix case `P0-OWN-01` covers duplicate launch, close and relaunch.

### Exact-head validation

- Fix commit: `94d23991e96f4480b59853db18f79ce9c7bd6036` (`fix: dispose App-owned lifecycle resources`). Follow-up commits add tests/docs and trigger a new full Windows workflow.
- Exact-head run [39764682027](https://github.com/frpboy/Scrobblix/actions/runs/39764682027) on SHA `b3b4aef20a34a8a4a8c3c39dc3d0fb7dd579b314` failed at build with `CS1729` because `App.xaml.cs` called `TrayIconService` with two arguments while this branch exposes a one-argument constructor. Fixed in commit `b626a16d76e04d5e26ee8bf8880de1407b186823`; the .NET build passed but the lifecycle contract step failed on exact whitespace in a startup-cleanup assertion. Fixed in test commit `5c018e2085f7567df2a8e813bd19f55414e27bb7`; full workflow on the current head is pending. Do not treat older run 39762602955 as validation for the lifecycle changes.
- Windows duplicate-start/close/relaunch retest: `NOT RUN`.
- Issue #6 should only be closed after exact-head CI succeeds and the manual lifecycle check is recorded or explicitly accepted as an outstanding Windows-only gate.

## Wider release gates

- Live qualifying playback → accepted Last.fm scrobble reconciliation, duplicate-free multi-day playback and 8–24 hour soak: `NOT RUN`.
- Durable queue delivery, ambiguous provider outcomes, bounded retry and restart recovery: automated assertions exist; real Windows/provider validation `NOT RUN`.
- Backup/restore and corrupt/interrupted restore scenarios: `NOT RUN` on a real Windows installation.
- Chrome/Edge/Firefox installation, actual extension IDs, native host connectivity and uninstall: `NOT RUN`.
- Tray/notification/Explorer restart, keyboard/Narrator, contrast/DPI and layout rendering: `NOT RUN` on Windows.
- Signed installer, clean install/upgrade/rollback/uninstall, SBOM and dependency/license review: `NOT RUN`.
- Stitch workspace: the Git handoff is not evidence of external Stitch changes; integration was not available during authoring.

## Evidence rules

A green CI run proves that exact commit built and passed the automated checks on its Windows runner only. It does not prove actual Last.fm acceptance, browser installation, GUI rendering/accessibility, signed distribution or upgrade/rollback. Manual tests remain `NOT RUN` until completed and evidenced.
