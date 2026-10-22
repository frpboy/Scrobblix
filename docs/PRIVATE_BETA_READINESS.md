# Private Beta Readiness

Updated: 2026-10-22  
Integration baseline: `main`

## Decision

Scrobblix has the main product flows implemented. The next milestone is **private beta readiness**, not feature expansion. The beta decision depends on real listening evidence and distribution safety.

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

- [ ] Implement a support bundle with a file/field preview and explicit export confirmation.
- [ ] Exclude Last.fm API key/shared secret/session key, SQLite/history, cached artwork, full URLs, unrelated logs, usernames and raw crash dumps by default.
- [ ] Seed canary secrets into log/diagnostic test inputs and verify no archive entry contains them.
- [ ] Verify archive is built atomically, temporary files are cleaned, the final hash/size is shown and the user chooses how to share it.

## Track E — Windows distribution

- [ ] Tray minimize/restore/Exit, Explorer restart icon recovery, notification activation/privacy and clean shutdown.
- [ ] Keyboard navigation, Narrator, contrast and 100–200% display scale.
- [ ] Prepare signed MSIX/App Installer or Store distribution.
- [ ] Verify publisher identity/signature/timestamp, clean install on another account/device, upgrade, repair, rollback, uninstall and post-uninstall data behavior.
- [ ] Complete dependency/license review, SBOM/provenance, supported environment matrix, release notes and known-issues page.

## Entry decision

Private beta remains **NO-GO** until P0 listening/queue acceptance and security-critical checks are passed and recorded. Unsigned development bundles may be shared only as explicitly labelled test builds with prerequisite and data-loss limitations.
