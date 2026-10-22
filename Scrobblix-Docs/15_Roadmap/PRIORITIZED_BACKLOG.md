# Prioritized Backlog

> **Updated:** 2026-10-22  
> **Integration baseline:** `main`  
> **Current milestone:** Private beta readiness

## Code inventory — present in the current mainline snapshot

The following capability surfaces or implementation paths exist in the codebase. This list is an inventory, **not a claim that every path has passed real-world qualification**:

- Home / current playback, eligibility progress, tracking pause/resume and Last.fm connection state.
- SQLite-backed local history, History search and CSV export.
- Statistics, Loved tracks, Queue, Sources, Diagnostics and Settings.
- GSMTC playback observation and source arbitration/canonical tracking.
- Last.fm authentication and provider actions (scrobble, Now Playing, Love and Unlove), with Credential Locker integration.
- Persistent queue, action leases/retries/recovery code and provider outcome states.
- Backup/restore logic with archive/checksum/schema/integrity validation and a pre-restore safety snapshot.
- Minimize-to-tray behavior and opt-in Windows notifications with separate track-detail privacy preference.
- Chromium / Firefox development manifests and Native Messaging installation helpers.
- Windows CI for .NET, assertion checks, browser extension build, manifest validation, publishing and development artifacts.

## P0 — Stabilize the working version

- [ ] Fetch `main` and check the exact HEAD SHA and latest Actions run before citing CI status.
- [ ] Execute and record `Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md` over multiple Spotify listening sessions.
- [ ] Verify qualifying submissions against the dedicated Last.fm test account; capture redacted correlation/action IDs and accepted provider evidence.
- [ ] Confirm repeat-after-intervening-track, pause/resume, seeking, ads, network loss, Last.fm outage, sleep/wake and restart produce neither lost nor duplicate scrobbles.
- [ ] Make Home explain current playback, eligibility countdown, queue status, provider acceptance and tracking-paused state without equating eligibility with delivery.
- [ ] Run a documented 8–24 hour soak.

## P1 — Reliability and security qualification

- [ ] Qualify queue recovery after process termination, lease expiry, network timeout after send, authentication loss and provider throttling.
- [ ] Test backup/restore against a disposable copy of non-production data, including corrupt archive, wrong schema, checksum mismatch and safety-snapshot restore.
- [ ] Review Native Messaging sender identity, framed message size/rate limits, message schema, allow-list fail-closed behavior, per-user registry/manifest ACLs, executable path trust and uninstall cleanup.
- [ ] Verify Chrome, Edge and Firefox event delivery, restart/reload/reconnect and source arbitration. Browser integration remains optional for desktop Spotify if GSMTC supplies sufficient metadata.
- [ ] Add a support-bundle export with preview and strict redaction tests: exclude Last.fm API/shared secrets, session keys, full listening DB/history, artwork cache, arbitrary full URLs, usernames and raw dumps by default.
- [ ] Verify Explorer restart/tray recovery, notification privacy/activation, lock-screen behavior, clean shutdown and crash/restart.
- [ ] Run keyboard, Narrator, contrast, DPI/scaling and responsive-layout checks on real Windows.

## P2 — Installable and maintainable release

- [ ] MSIX/App Installer packaging and update strategy.
- [ ] Publisher identity/certificate or Store identity; sign and timestamp every distributed package.
- [ ] Clean install on another Windows account/device; upgrade, repair, rollback and uninstall/residual-data checks.
- [ ] Dependency/license inventory, SBOM, provenance and vulnerability review.
- [ ] First-run onboarding, source health clarity, per-track history detail, refined notifications, startup/tray preferences and statistics polish.
- [ ] Supported-environment matrix, release notes, known issues, privacy/data-removal docs, troubleshooting and beta support process.

## Out of scope for v1

Cloud sync, Discord, multiple providers, plugin system, user scripting/rules, mobile/macOS/Linux clients and a Spotify Web API dependency.
