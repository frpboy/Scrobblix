# Tray context-menu implementation

Issue: https://github.com/frpboy/Scrobblix/issues/49  
Branch: feat/tray-quick-controls  
Status: Implementation candidate; actual Windows acceptance remains NOT RUN until executed.

## Design

Scrobblix retains its existing native Win32 notification-area icon and popup implementation in `TrayIconService`. The menu is rebuilt from the current `MainWindow` state each time it opens rather than reusing a startup-only snapshot. It restores and navigates the existing window and does not create a second instance or a new tray framework.

Menu entries:

- Disabled status rows: Scrobblix running, Last.fm account state, current playback state, and current pending queue count.
- Open Scrobblix and Pause Tracking / Resume Tracking.
- Current Track submenu: current metadata/status, Love Track / Unlove Track, and Copy Track Details.
- Scrobble Queue submenu: real queue-state counts, retryable failed-scrobble count, Retry Failed Scrobbles, and Open Queue.
- Open Settings, Open Diagnostics, Open Log Folder, and Exit Scrobblix.

## Safety and state rules

- Playback status uses the displayed observation only when artist/title metadata is present and playback is Playing or Paused. Long menu strings are truncated; clipboard copy retains the full plain-text `Artist — Track` representation. No credentials or session keys are copied.
- Tracking controls call the same existing tracking-mode transition and preserve local history/eligible queued work.
- Love/Unlove uses the existing application event and durable queue writer. Pending and in-flight actions block repeated current-track requests; the menu status distinguishes a queued request from provider acceptance and unknown outcomes.
- The retry query returns only `provider=lastfm`, `action_type=Scrobble`, `state=RetryExhausted`. It excludes scheduled retries, accepted records, Love/Unlove actions, other providers, and `SubmissionOutcomeUnknown`; the existing atomic `RetryNowAsync` store transition performs the user-requested retry.
- Open Log Folder uses the configured current log directory and `ProcessStartInfo` with `UseShellExecute`; absent paths and launch failures are handled without crashing.
- The existing window close handler remains the single shutdown pathway; shutdown state guards tray-originated commands and the native menu/subclass/icon are disposed by the existing lifecycle.

## Verification record

- Automated regression coverage added for the durable retry-candidate read model, specifically checking retry-exhausted scrobbles against accepted actions, scheduled retries, unknown outcomes, Love actions, and other providers.
- Existing tracking-mode and manual queue retry tests remain in the test executable.
- Build and CI status: see the pull request's exact-head checks; do not infer Windows UI qualification from CI.
- Manual Windows acceptance: **NOT RUN** in this environment. Use section H of `Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md`; keep every unexecuted case as NOT RUN and record the exact candidate SHA before any manual qualification claim.
