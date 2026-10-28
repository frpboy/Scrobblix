# Private Beta Readiness

Updated: 2026-10-28  
Integration baseline: `main`

## Decision

Scrobblix has the main product flows implemented, but release qualification remains incomplete. Older local logs captured Browser Companion named-pipe `IOException` bursts and a rebuild collision with a still-running desktop process. PR #7 was merged on 2026-10-22 with the private-beta readiness changes, but those historical logs do not prove the same symptoms still reproduce on today's `main`. Retest the exact current candidate on Windows and capture exit/Event Viewer evidence if it fails; do not claim the crash is fixed or still present solely from the old logs.

## Track 0 — crash and startup stability (P0)

- [ ] Fetch the current `main` (or explicitly nominated candidate) SHA and build/launch that exact commit; record its matching Windows Actions run. Do not rely on a branch name or a historical green run.
- [ ] Verify the launcher refuses to rebuild while `Scrobblix.Desktop` is open, including a minimized-to-tray instance, and never terminates a user process.
- [ ] Reproduce Browser Companion/native-host pipe contention; verify bounded exponential retry, warning throttling, suppressed counts, and successful reconnect.
- [ ] Repeat startup, normal exit, close-to-tray, restart, and browser restart. Reconcile history and queue/action IDs to prove no track loss or duplicate acceptance.
- [ ] If the process still exits unexpectedly, capture exact exit code, Windows Application event and timestamp-aligned sanitized log tail; collect a dump only if Windows produces one. Do not mark the crash fixed merely because CI passes.

 The next milestone is **private beta readiness**, not feature expansion. The beta decision depends on real listening evidence and distribution safety.

## Track A — prove basic listening correctness

- [ ] Run several Spotify desktop sessions across multiple days.
- [ ] Record song changes, pause/resume, seek, repeated tracks, Spotify ads, sleep/wake, network loss/recovery, Last.fm temporary failure, Scrobblix restart and repeated event delivery.
- [ ] Match each local qualifying play/action to the Last.fm test profile and accepted queue state using redacted correlation IDs.
- [ ] Confirm the same-track repeat after an intervening track produces a new scrobble while duplicate notifications for one play do not.
- [ ] Record any loss, duplicate, unexplained state, stale countdown or tracking gap as a defect; do not waive it because the UI looks plausible.

## Track B — endurance and recovery

- [ ] Run an 8–24 hour continuous session.
- [ ] Record process memory/CPU samples, SQLite size, log growth, UI responsiveness, source changes, queue counts and errors at start/end and periodically during the soak.
- [ ] Force offline/online, restart the app, suspend/resume Windows and recover Last.fm authorization; verify no qualifying play silently disappears or duplicates.
- [ ] Save a backup from disposable test data, restore it, and test corrupt ZIP/checksum/schema mismatch plus pre-restore snapshot recovery.

## Track C — Browser Companion (optional for desktop Spotify)

- [ ] Chrome installation/registration, page event delivery, pause/seek/track changes, browser reload/restart and uninstall.
- [ ] Edge installation/registration separately, including its actual extension ID.
- [ ] Firefox temporary add-on, configured add-on identity, delivery/restart and removal.
- [ ] Verify allow-list rejection for wrong identities, invalid payloads and disconnect/reconnect.
- [ ] Sources UI clearly distinguishes desktop GSMTC observations from optional browser events, and does not call a source connected merely because files exist.

## Track D — supportability and privacy

- [ ] Qualify the Diagnostics export in a real Windows session: verify confirmation text, Save/cancel behavior, ZIP integrity/hash, UI status, no auto-upload and accessibility. Run seeded-canary redaction assertions in CI. The format-version-2 bundle now includes only normalized source kind, playback state, last-observation UTC time and tracking enabled/paused in addition to sanitized operational events.
- [ ] Exclude Last.fm API key/shared secret/session key, SQLite/history, cached artwork, full URLs, unrelated logs, usernames and raw crash dumps by default.
- [ ] Seed canary secrets into log/diagnostic test inputs and verify no archive entry contains them; test forged source/state strings normalize to fixed allow-list values.
- [ ] Verify archive is built atomically, temporary files are cleaned, the final hash/size is shown and the user chooses how to share it.

## Track E — UI/design qualification

The lightweight Home hierarchy from the Stitch handoff is now reflected in native WinUI: current track and eligibility are primary, Last.fm and queue state share one delivery section, Today is concise, and Home shows three recent tracks. The separate Sources and Queue pages remain intact. The live InfoBar title also follows startup, playback, pause/resume and storage-error states. See [Stitch UI handoff](STITCH_UI_HANDOFF_2026-10-22.md). This is a source implementation update, not a claim of completed Windows visual qualification.

- [ ] Review the actual WinUI Home and Sources screens on Windows against the Stitch references at 100%, 150% and 200% scale.
- [ ] Confirm the Home hierarchy remains simple while countdown, queue/retry, provider acceptance and tracking pause states stay visible and truthful.
- [ ] Check keyboard focus, Narrator labels, contrast, resize behavior and no clipping of status strings. Stitch HTML/screenshot exports are references, not shipped application code.

## Track F — Windows distribution

- [ ] Tray minimize/restore/Exit, Explorer restart icon recovery, notification activation/privacy and clean shutdown.
- [ ] Keyboard navigation, Narrator, contrast and 100–200% display scale.
- [ ] Prepare signed MSIX/App Installer or Store distribution.
- [ ] Verify publisher identity/signature/timestamp, clean install on another account/device, upgrade, repair, rollback, uninstall and post-uninstall data behavior.
- [ ] Complete dependency/license review, SBOM/provenance, supported environment matrix, release notes and known-issues page.

## Entry decision

Private beta remains **NO-GO** until P0 listening/queue acceptance and security-critical checks are passed and recorded. Unsigned development bundles may be shared only as explicitly labelled test builds with prerequisite and data-loss limitations.
