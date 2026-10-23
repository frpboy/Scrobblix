# Windows tray quick controls

**Status:** Implemented on the feature branch; Windows interaction acceptance remains NOT RUN until exercised on an installed Windows build.

## Native menu

The existing custom Win32 notification-area implementation remains in use through Shell_NotifyIcon, window subclassing, and TrackPopupMenu. The Windows 11 notification-area overflow surface itself is owned by Windows and is not changed.

Each time the context menu opens, Scrobblix builds a fresh menu from an immutable TrayMenuStatus snapshot. Rendering does not query SQLite, call Last.fm, or poll inside the menu callback. The snapshot is built from values already held by the main window and the latest queue read model.

The menu contains:

- Disabled status rows: Scrobblix running state, Last.fm connection/account, current playback and pending queue count. Unknown queue state is labelled unavailable, not shown as zero.
- Open Scrobblix: restores and activates the existing window.
- Pause Tracking / Resume Tracking: calls the same tracking-state transition used by the main window. It stops/starts observation only; it does not clear eligible listening history or queue records.
- Current Track: the latest local Love/Unlove intent/status, Love or Unlove using the existing event/service path, and Copy Track Details. Provider acceptance is not implied by a locally queued intent.
- Scrobble Queue: pending, in-flight, retry-scheduled, accepted, authentication-blocked, permanent-failure, and outcome-unknown counts when available; Retry Failed Scrobbles only for eligible exhausted scrobbles; and Open Queue.
- Open Log Folder, Open Settings, Open Diagnostics, and Exit Scrobblix.

## Queue retry boundary

The queue reader exposes a read-only query for Last.fm Scrobble actions in RetryExhausted state. The tray action re-reads eligible action IDs, then uses the existing queue store's atomic RetryFailedScrobbleNowAsync transition, which requires Last.fm + Scrobble + RetryExhausted at mutation time. Therefore, an action claimed or otherwise changed since the read is not blindly resubmitted. Scheduled retries keep their backoff; accepted, uncertain, Love/Unlove and non-scrobble actions are excluded.

## Lifecycle, errors and privacy

- The existing single-instance activation and restore path is preserved.
- The native popup is destroyed after selection; the existing tray icon/window subclass cleanup remains idempotent.
- Tracking and asynchronous queue/Love actions use existing application event/service routes and guard against rapid repeat clicks.
- Clipboard content is plain Artist — Track; failures are logged by exception type only.
- Explorer launch uses the logging directory resolved by the app. Missing-directory and launch failures are reported without creating arbitrary folders.
- Account secrets and session keys are never placed into menu labels or logs.
- Menu state refreshes on the next opening using the newest local snapshots; there is no aggressive polling or new event subscription to leak.

## Automated coverage

The existing deterministic console test suite now checks truthful connected/disconnected labels, tracking label transitions, missing queue state, long metadata truncation, Love action enablement and duplicate guards, retry eligibility, local intent wording, paused/stopped playback distinction, and disposed-window guards.

Run the repository's existing CI workflow for authoritative Windows build and test results. Do not substitute a test report from another SHA.

## Manual acceptance

The full real-machine matrix is maintained in Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md, section F.1 Windows tray quick controls. Every entry remains NOT RUN until tested against the exact installed candidate SHA. This includes Last.fm connect/disconnect and live Love/Unlove, tracking pause with queued scrobbles, Windows Explorer/clipboard behavior, tray startup/shutdown, accessibility and single-instance activation.
