# Real-world Manual Test Matrix

> **Purpose:** Produce auditable evidence for private beta readiness.  
> **Status at creation:** Not run in this session; the connected GitHub tools cannot exercise your Windows player, Last.fm account, Windows shell or installed browsers.  
> **Baseline:** `main`; record the exact candidate commit used for every test.  
> **Last updated:** 2026-10-22

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
| BR-01 | Load extension and register exact ID; restart extension/browser/desktop app. | Native host connects; no manual assumptions about IDs; source state reflects successful events, not manifest presence. | NOT RUN | |
| BR-02 | Play/pause/seek/skip in a supported player and compare page events to desktop history. | Metadata/state reaches Scrobblix with reasonable latency; one canonical play. | NOT RUN | |
| BR-03 | Reload tab/extension, close/reopen browser, suspend tab, revoke permission. | Reconnect/snapshot recovery works or Source page explains the offline/unavailable state. | NOT RUN | |
| BR-04 | Use a forged/wrong extension identity and malformed/oversized message. | Native host/desktop reject it; app remains responsive; logs redact untrusted payloads. | NOT RUN | |
| BR-05 | Uninstall host registration and remove extension/add-on. | Per-user registration is removed; no stale host process continues accepting messages. | NOT RUN | |
| BR-06 | Use Spotify desktop with browser extension not installed. | GSMTC still works; UI explains the browser companion is optional. | NOT RUN | |

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

## G. Support bundle safety and usability

| ID | Scenario and steps | Expected result | Result | Evidence / defect |
|---|---|---|---|---|
| SB-01 | Open Diagnostics and choose Export support bundle; review the confirmation before saving. | Exact allow-listed files/fields and exclusions are described; no automatic upload is implied. | NOT RUN | |
| SB-02 | Export to a new ZIP and inspect every entry with a ZIP viewer. | Only `manifest.json`, `event-summary.txt`, `operational-events.tsv` and `README.txt` exist; raw logs, database, artwork and preference files are absent. | NOT RUN | |
| SB-03 | Compare the displayed SHA-256 with a locally calculated ZIP hash. | Hashes match, ZIP opens cleanly and event count matches the manifest/status. | NOT RUN | |
| SB-04 | Cancel the confirmation or the Save dialog. | No support archive is generated or uploaded; diagnostics UI remains usable. | NOT RUN | |
| SB-05 | Seed a test log with fake credentials, session key, token, artist/title, URL and username/path canaries. | Automated redaction assertions pass and none of the canaries appear in any archive entry. | NOT RUN | |
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
