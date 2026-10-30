# Real-world Manual Test Matrix

> **Purpose:** Produce auditable evidence for private beta readiness.  
> **Manual status:** All real-machine, provider, installed-browser, UX and installer scenarios remain `NOT RUN`; repository CI does not set these rows to PASS.  
> **Historical code-candidate CI:** SHA `23bc40a9e7a959145d3494fb7e6382d079020710` passed Windows workflow [run 39732158406](https://github.com/frpboy/Scrobblix/actions/runs/39732158406) on 2026-10-22. This is historical evidence only. PR #7 was merged on 2026-10-22, but its old CI result does not validate current main. Before each new test run, verify the current candidate SHA and matching Actions run; do not infer current status from a branch label or older run.  
> **Baseline rule:** Fill the run header using the exact app build installed for the manual test; do not carry the CI success into manual results.  
> **Last updated:** 2026-10-30

## Run header

Fill before testing; do not include secrets.

| Field | Value |
|---|---|
| Tester / machine label | Not recorded |
| Windows edition/build | Not run |
| Scrobblix commit SHA / app version | Not run |
| Last.fm test account alias (not credentials) | Not run |
| Spotify desktop version | Not run |
| Browser versions and extension IDs (IDs only) | Not run |
| Test start/end local time + timezone | Not run |
| Actions run URL for exact candidate SHA | Not run |
| Result log / sanitized evidence location | Not run |

**Result vocabulary:** `PASS`, `FAIL`, `BLOCKED`, `NOT RUN`. Leave tests as `NOT RUN` until performed. A planned test is not a pass.

## A0. Crash, launch and logging stability

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| CR-01 | Close Scrobblix normally, including the tray instance, then use `scripts/update-and-run.ps1` to rebuild and launch. Repeat once with the app deliberately left open. | Closed-app run builds and launches. Open-app run stops before restore/build, lists the PID, and never kills the process automatically. | NOT RUN | |
| CR-02 | Enable the Browser Companion and reproduce native-host connect attempts while Scrobblix is starting, stopping, or busy processing a message. | No hot loop; I/O retry uses bounded backoff; same-category warnings are throttled and include the suppressed count. Browser support remains optional for desktop Spotify. | NOT RUN | |
| CR-03 | Restart Scrobblix and the browser/extension after a busy-pipe period. | The listener reconnects and the failure backoff resets after a successful connection; retries do not create a duplicate play or provider action. | NOT RUN | |
| CR-04 | If the process exits unexpectedly, capture exact app SHA/build, exit code, Windows Application event and timestamp-aligned final 100 sanitized log lines; collect a crash dump only if produced. | A fatal exception or OS-level cause is recorded before root cause is declared; secrets and listening content are redacted from shared evidence. | NOT RUN | |
| P0-OWN-01 | Launch normally, attempt a second launch, close the running instance, then relaunch. Exercise window-construction failure where feasible. | The second launch signals the first instance and disposes temporary handles; startup failure and normal close stop the listener, dispose the tray icon and synchronization handles, and allow a fresh launch to acquire the gate. | NOT RUN | |

## A. Spotify desktop playback — run across multiple days

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| SP-01 | Start Spotify and play a normal track; observe Home and local History. | One canonical play appears; artist/title/source and confirmed time update without waiting for Last.fm eligibility. | NOT RUN | |
| SP-02 | Let a track cross its eligibility threshold. | UI countdown reaches eligibility; exactly one queue action is created and later provider result is explicit. | NOT RUN | |
| SP-03 | Pause before eligibility, wait 60 seconds, resume. | Confirmed played time does not increase while paused; remaining time resumes from the previously confirmed amount. | NOT RUN | |
| SP-04 | Seek forward and backward; compare media position to confirmed played time. | Seeking does not count skipped time as listened and does not create duplicate play sessions. | NOT RUN | |
| SP-05 | Play track A, B, then A again until eligible. | The second A is a distinct qualifying play and may scrobble once; it must not be mistaken for a duplicate of the earlier A. | NOT RUN | |
| SP-06 | Trigger repeated/duplicate GSMTC observations for the same currently playing track (where reproducible). | One canonical play/action; duplicate observations are merged. | NOT RUN | |
| SP-07 | Observe Spotify ad playback. | Ads/non-music do not create a Last.fm music scrobble; status/reason is understandable. | NOT RUN | |
| SP-08 | Switch tracks rapidly, including skip before threshold. | Short/unfinished tracks remain local history where expected but do not scrobble; the new track is tracked promptly. | NOT RUN | |
| SP-09 | Lock the workstation, unlock and resume playback. | Tracking recovers; no unsupported time accrues during suspended playback. | NOT RUN | |
| SP-10 | Sleep Windows for several minutes, wake and continue listening. | GSMTC polling/events recover without inflated listening time or repeated submission. | NOT RUN | |
| SP-11 | Leave Spotify playing while temporarily disconnecting the network, then reconnect. | Local history continues; qualifying actions remain safely queued and delivery resumes without loss/duplicate. | NOT RUN | |
| SP-12 | Keep playing with Last.fm temporarily unavailable (service/network failure), then restore service. | Home says queued/waiting or retrying; the app does not falsely say “Sent”; recovery drains the queue safely. | NOT RUN | |
| SP-13 | Quit/reopen Scrobblix while a track is eligible and an action is pending/retrying. | Queue recovery is durable; no duplicate submission or silently lost qualified play. | NOT RUN | |
| SP-14 | Restart Scrobblix during/after the same track; replay or re-observe the same metadata. | Idempotency and persisted play identity prevent resending the same canonical play. | NOT RUN | |
| SP-15 | Let a track end before threshold. | UI reports “Not eligible — played too briefly” only when that is the actual decision; duration/metadata issues use a different reason. | NOT RUN | |
| SP-16 | Pause tracking in Scrobblix, keep Spotify active, then resume. | UI clearly says “Tracking paused”; no listening time is added while paused; queued actions remain unchanged. | NOT RUN | |

## B. Last.fm provider outcome reconciliation

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| LF-01 | Connect using a dedicated test account and complete authorization. | Connected user shown; session key kept in Windows Credential Locker, not SQLite/logs. | NOT RUN | |
| LF-02 | Submit a qualifying test play; inspect Last.fm profile after normal propagation delay. | Profile contains the expected scrobble, with matching artist/title/timestamp; action state is provider-accepted. | NOT RUN | |
| LF-03 | Compare local accepted queue action IDs and test-window Last.fm profile history. | Each accepted scrobble maps to one intended play; document any mismatch. | NOT RUN | |
| LF-04 | Revoke/expire session or make credentials invalid in a controlled test. | Actions remain visible as auth-blocked; reconnection allows safe recovery. | NOT RUN | |
| LF-05 | Simulate or observe rate limit/temporary provider failure. | Retry/backoff is bounded and transparent; no tight retry loop or silent discard. | NOT RUN | |
| LF-06 | Test ambiguous timeout after request transmission, then inspect profile before choosing retry. | Outcome-unknown is not treated as definitely failed or definitely accepted; no blind duplicate. | NOT RUN | |
| LF-07 | Verify Love/Unlove and Now Playing actions separately. | Correct action types/outcomes; these actions are not counted as accepted scrobbles. | NOT RUN | |

## C. Queue / duplicate / storage recovery

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| QR-01 | Stop the app while a pending action exists; restart. | Pending/retry items are discovered and processed after restart. | NOT RUN | |
| QR-02 | Kill the app during a provider request in a dedicated test scenario. | Expired lease recovers; possible ambiguous acceptance is handled conservatively. | NOT RUN | |
| QR-03 | Observe the same play from GSMTC and browser within arbitration window (once browser test is ready). | One canonical play and at most one scrobble action. | NOT RUN | |
| QR-04 | Fill the queue with a test backlog while offline; bring the network back. | Queue drains with bounded pacing/backoff; app stays responsive; ordering/retry is sane. | NOT RUN | |
| QR-05 | Export/backup disposable test DB, restore into test location. | Restore verifies archive/schema/integrity and creates a pre-restore safety snapshot. | NOT RUN | |
| QR-06 | Test corrupt archive, checksum mismatch, unsupported schema and interrupted restore using disposable copies only. | Restore fails closed without overwriting good live data; safety snapshot remains usable. | NOT RUN | |

## D. 8–24 hour soak

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| SO-01 | Leave Scrobblix tracking for at least 8 hours; preferably repeat at 24 hours. Record resource/log/database metrics periodically. | No unbounded memory/CPU/log growth, dead UI, duplicate actions or unexplained tracking gaps. | NOT RUN | |
| SO-02 | During the soak, include track changes, pause/resume, screen lock, sleep/wake, network and Last.fm interruptions. | Each interruption recovers and is noted; queue/data integrity persists. | NOT RUN | |
| SO-03 | Restart app once during the soak and resume same track. | Recovery works and the same canonical play is not scrobbled twice. | NOT RUN | |

## E. Browser Companion — optional for Spotify desktop

Run separately for Chrome and Edge with distinct actual extension IDs, then Firefox with its actual temporary add-on identity.

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| BR-01 | Load the unpacked extension in Chrome and Edge separately; record both actual IDs; register using `-ExtensionId @("CHROME_ID","EDGE_ID")`; restart both browsers and Scrobblix. | Each browser's exact origin is allow-listed; neither browser is assumed to share the other's ID; source state reflects actual events, not manifest presence. | NOT RUN | |
| BR-02 | Play/pause/seek/skip/end in a supported player; compare page events to desktop History. | Valid metadata and playback state reach Scrobblix with reasonable latency; one canonical play. | NOT RUN | |
| BR-03 | Reload tab/extension, close/reopen browser, suspend tab, revoke permission, restart native host and restart Scrobblix. | Reconnect/re-observation works or Sources explains the offline/unavailable state; no duplicate canonical play. | NOT RUN | |
| BR-07 | Register a custom site and start playback. Trigger a track-change observation, then remove the site while custom-site authorization is pending (repeat the race in an instrumented/test build if needed). Record the popup removal acknowledgement and inspect the native-host receive log for that origin. | After the removal acknowledgement, no in-flight or subsequent observation from the removed origin reaches Native Messaging; the content script is unregistered and the exact browser permission is revoked. Pre-acknowledgement delivery is recorded separately rather than mislabeled as a post-removal leak. | NOT RUN | |
| BR-04 | Use an unsupported origin, forged/wrong extension identity, forged ping, malformed/oversized frame, invalid field types, oversized strings and non-HTTPS artwork. | Service worker/native host reject invalid input before forwarding/responding; app remains responsive and raw untrusted payloads are not logged. | NOT RUN | |
| BR-05 | Run the uninstall script after closing browsers, then inspect HKCU registration, allow-list values and `%LOCALAPPDATA%\Scrobblix\NativeHost`. | Browser registration and trust IDs are removed; the dedicated host directory is deleted when not locked; history and logs remain untouched. | NOT RUN | |
| BR-06 | Use Spotify desktop with the browser extension not installed. | GSMTC still works; UI explains the Browser Companion is optional. | NOT RUN | |

## F. Windows UX, notifications and privacy

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| UX-01 | Minimize, restore by tray double-click, use tray Open and Exit. | App is restored/closed intentionally; queue and DB shutdown behavior is safe. | NOT RUN | |
| UX-02 | Restart Windows Explorer while app is running. | Tray icon recovers or the UI/log gives a recoverable error; no second app needed. | NOT RUN | |
| UX-03 | Enable/disable notifications; restart app; trigger accepted scrobble notification. | Choice persists; disabled-by-default behavior is respected; success notices require provider acceptance. | NOT RUN | |
| UX-04 | Leave track-detail privacy disabled; test Windows notification surface/lock screen. | Artist/title not shown in notification; toggle controls this separately and choice persists. | NOT RUN | |
| UX-05 | Use keyboard only; navigate main screens, dialogs and queue actions. | Visible focus and predictable order; no mouse-only interaction blocks core flows. | NOT RUN | |
| UX-06 | Use Narrator, contrast theme and 100/150/200% display scale. | Labels, status changes, controls and content remain perceivable/operable. | NOT RUN | |
| UX-07 | Toggle tracking off while a track plays, then on. | “Tracking paused” is explicit; no listening time accrued during pause; queue worker state remains separate. | NOT RUN | |

| UX-08 | Open Sources before playback, then play in Spotify desktop and a supported browser player if installed. Compare the displayed source and time with actual event delivery. | Empty state stays honest; GSMTC/browser changes only after an observed event. Presence of extension files or a listener is not reported as connected playback. | NOT RUN | |
| UX-09 | Review Home with no playback, active playback, tracking paused, queued delivery, retry, provider acceptance and Last.fm disconnected. Resize and test at 100%, 150% and 200% scale. | Now Playing is the primary focal point; eligibility progress reflects confirmed listening; queue/provider states stay distinct and truthful; Home shows no more than three recent tracks; no clipping or duplicate Sources/Queue tiles. | NOT RUN | |
| UX-10 | Collapse and expand the rail from Home, History, Queue, Sources and Settings using mouse and keyboard. Repeat while a track is actively playing and while a queue action is being delivered. | Selected destination remains unchanged, all eight destinations remain reachable, and live tracking, confirmed listening and queue delivery continue without interruption. | NOT RUN | |
| UX-11 | Resize the window around 960–1000 logical pixels; change the preference, widen again, restart the app, then activate via tray and notification. | Narrow window temporarily uses compact mode without overwriting the saved preference. Wide window restores preference. Window title and selected destination identify the active page after activation. | NOT RUN | |
| UX-12 | Use keyboard-only navigation and Narrator. Test 100%, 150% and 200% scale plus Windows light/dark/high-contrast settings. Inspect the app icon, compact privacy tooltip and source-logo fallback. | Rail toggles and destinations have clear names and focus; no clipping; mark variants change where supported; status is not communicated by color/logo alone; unknown or unapproved sources use a neutral icon and text. | NOT RUN | |
| UX-13 | Compare Home, History, Statistics, Queue, Sources, Diagnostics and Settings at the normal window size, a narrow window, and maximized. Check page margins, card gaps, the Home status banner, and the current-track action row. | Outer content inset is compact and consistent, cards retain breathing room, routine playback does not leave a redundant banner taking vertical space, tracking/Love/Unlove controls fit without clipping, no horizontal scrolling is introduced, and attention banners still appear for actionable errors or tracking pause/resume. | NOT RUN | |

## F.1 Windows tray quick controls

Run on the exact installed candidate SHA above. Automated assertions and a successful Release build do not substitute for these real Windows checks.

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| TRAY-01 | Open the context menu with no active media, Last.fm disconnected, and an empty queue; repeat with the DB temporarily unavailable. | Status is truthful, unknown queue counts are shown as unavailable rather than zero, and unavailable actions are disabled without crashing. | NOT RUN | |
| TRAY-02 | Play, pause, change, and stop a track; include missing and very long artist/title values. Reopen the menu after each change. | Playback/account status follows the latest window snapshot; long values are truncated; a stopped/no-track state does not retain stale current-track actions. | NOT RUN | |
| TRAY-03 | Pause tracking in the tray, keep media playing, then resume. Seed an already eligible persisted scrobble before pausing. | The label follows the confirmed tracking state; no listening time accrues while paused; previously queued work and persisted history remain intact. | NOT RUN | |
| TRAY-04 | With Last.fm disconnected, attempt Love/Unlove; then connect a dedicated test account and Love/Unlove a supported current track. | Disconnected action is disabled. Connected actions use the existing queue pathway, reject rapid duplicates, and distinguish locally requested intent from provider acceptance/rejection. | NOT RUN | |
| TRAY-05 | Seed retry-exhausted scrobble actions, scheduled retries, successful submissions, and outcome-unknown actions. Use Retry Failed Scrobbles. | Only retry-exhausted Last.fm scrobble actions are eligible; the atomic RetryFailedScrobbleNowAsync state check remains authoritative; scheduled backoff, successful submissions, Love actions, and uncertain outcomes are untouched. | NOT RUN | |
| TRAY-06 | Choose Open Queue, Open Settings and Open Diagnostics while the window is minimized/hidden. | The existing application window is restored and navigates to the requested page; no duplicate process/window is launched. | NOT RUN | |
| TRAY-07 | Choose Open Log Folder with logs present, then test a missing/denied folder; copy details for a known track and force clipboard failure where feasible. | Explorer opens the resolved log directory or a recoverable message is shown. Clipboard copies only Artist, Track; failures do not crash or log track/credential data. | NOT RUN | |
| TRAY-08 | Exit from the tray while idle and while background tracking/queue work is active; relaunch, then activate through a second launch attempt. | Existing close/cancellation/disposal path runs once, persisted history and queue survive, relaunch works, and activation restores the existing instance. | NOT RUN | |
| TRAY-09 | Add/retry/deliver/fail queue actions and change Last.fm account state, then reopen the tray menu. | Menu uses refreshed read-model state and does not report a provider action accepted merely because it was queued. No aggressive polling or duplicate event subscriptions are introduced. | NOT RUN | |

## G. Support bundle safety and usability

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| SB-01 | Open Diagnostics and choose Export support bundle; review the confirmation before saving. | Exact allow-listed files/fields and exclusions are described; no automatic upload is implied. | NOT RUN | |
| SB-02 | Export to a new ZIP and inspect every entry with a ZIP viewer. | Only `manifest.json`, `event-summary.txt`, `operational-events.tsv` and `README.txt` exist; raw logs, database, artwork and preference files are absent. Verify `manifest.json` format version 2 includes only normalized source kind, playback state, last-observation UTC time and tracking enabled/paused; no artist/title, site URL, extension ID or arbitrary source text. | NOT RUN | |
| SB-03 | Compare the displayed SHA-256 with a locally calculated ZIP hash. | Hashes match, ZIP opens cleanly and event count matches the manifest/status. | NOT RUN | |
| SB-04 | Cancel the confirmation or the Save dialog. | No support archive is generated or uploaded; diagnostics UI remains usable. | NOT RUN | |
| SB-05 | Seed a test log with fake credentials, session key, token, artist/title, URL and username/path canaries; also pass forged source/state strings to the exporter in an automated test. | Automated redaction assertions pass; forged values normalize to fixed `other` / `unknown` values, and none of the canaries appear in any archive entry. | NOT RUN | |
| SB-06 | Export with an empty log folder, a source log locked/rotated during capture, and a denied output location. | Empty/partial-source scenarios fail safely or produce a clearly reported minimal bundle; destination failures do not corrupt an existing archive. | NOT RUN | |
| SB-07 | Navigate to the Diagnostics export with keyboard and Narrator; inspect at 100%, 150% and 200% scale. | Dialog, save action, status and errors are accessible; no mouse-only operation is required. | NOT RUN | |

## H. Private-beta decision summary

| Gate | Result | Evidence link / notes |
|---|---|---|
| Exact candidate CI | NOT RUN | |
| Spotify multi-day matrix | NOT RUN | |
| Last.fm profile reconciliation | NOT RUN | |
| Duplicate/restart recovery | NOT RUN | |
| 8–24 hour soak | NOT RUN | |
| Backup/restore on disposable data | NOT RUN | |
| Chrome + Edge + Firefox (or browser companion explicitly omitted from beta) | NOT RUN | |
| Native Messaging security review | NOT RUN | |
| Support-bundle canary test + Windows export review | NOT RUN | |
| Windows tray/notifications/accessibility | NOT RUN | |
| Signed install/upgrade/rollback/uninstall | NOT RUN | |

**No-go rule:** a failed P0 case or unexplained mismatch between qualifying local plays and Last.fm history blocks private beta until the defect is fixed or an explicit, documented mitigation is approved.
