# Windows tray quick controls

**Status:** Implemented on the feature branch; Windows interaction acceptance remains NOT RUN until exercised on an installed Windows build.

## Native menu

The existing custom Win32 notification-area implementation remains in use (\`Shell_NotifyIcon\`, window subclassing and \`TrackPopupMenu\`). The Windows 11 notification-area overflow surface itself is owned by Windows and is not changed.

Each time the context menu opens, Scrobblix builds a fresh menu from an immutable \`TrayMenuStatus\` snapshot. Rendering does not query SQLite, call Last.fm, or poll in the menu callback. The snapshot is built from values already held by the main window and the latest queue read model.

The menu contains:

- Disabled status rows: Scrobblix running state, Last.fm connection/account, current playback and pending queue count. Unknown queue state is labelled unavailable, not shown as zero.
- Open Scrobblix: restores and activates the existing window.
- Pause Tracking / Resume Tracking: calls the same tracking-state transition used by the main window. It stops/starts observation only; it does not clear eligible listening history or queue records.
- Current Track: the most recent trustworthy local Love/Unlove intent/status, Love or Unlove using the existing event/service path, and Copy Track Details. Provider acceptance is not implied by a locally queued intent.
- Scrobble Queue: pending count, Retry Failed Scrobbles when eligible actions exist, and Open Queue.
- Open Log Folder, Open Settings, Open Diagnostics, and Exit Scrobblix.

## Queue retry boundary

The queue reader exposes a read-only query for Last.fm \`Scrobble\` actions in \`RetryExhausted\` state. The tray action re-reads eligible action IDs, then passes each ID through the existing \`SqliteProviderActionStore.RetryNowAsync\` state check. Therefore, an action claimed or otherwise changed since the read is not blindly resubmitted. Scheduled retries keep their backoff; accepted, uncertain, Love/Unlove and non-scrobble actions are excluded.

## Lifecycle, errors and privacy

- The existing single-instance activation event and restore path are preserved.
- The native popup is destroyed after selection; the existing tray icon/window subclass cleanup remains idempotent.
- Tracking and asynchronous queue/Love actions use the existing application event/service routes and guard against repeated tray clicks.
- Clipboard content is plain \`Artist — Track\`; failures are logged by exception type only.
- Explorer launch only uses the logging directory resolved by the app. Missing-directory and launch failures are reported without creating arbitrary folders.
- Account secrets/session keys are never placed into menu labels or logs.
- The menu state refreshes on the next opening using the newest local snapshots; there is no aggressive polling or new event subscription to leak.

## Automated coverage

The existing deterministic console test suite now checks truthful connected/disconnected labels, tracking label transitions, missing queue state, long metadata truncation, Love action enablement and no-duplicate gates, retry eligibility, local intent wording, and disposed-window guards.

Run the repository's existing CI workflow for the authoritative Windows build and test results. This environment must not substitute a test report from another SHA.

## Manual acceptance

The full real-machine matrix is maintained in \`Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md\`, section **F.1 Windows tray quick controls**. Every entry remains NOT RUN until tested against the exact installed candidate SHA. This includes Last.fm connect/disconnect and live Love/Unlove, tracking pause with queued scrobbles, Windows Explorer/clipboard behavior, tray startup/shutdown, accessibility and single-instance activation.
