# Scrobblix v1 — implementation and release status

Updated: 2026-10-22 (UTC)
Repository: https://github.com/frpboy/Scrobblix
Default branch: `main`
Current main SHA at this checkpoint: `0436ed83762971ef75dae5be1ffd15bab7e1eae5`
Current main CI: [run 39812019297](https://github.com/frpboy/Scrobblix/actions/runs/39812019297) — **IN PROGRESS at time of this edit**. Do not call this SHA green until this exact run completes successfully.

## Current implementation state

The earlier checkpoint that described PR #5 as open and CA1001 as the current blocker was stale. Do not use it as a current release assessment.

Confirmed merged implementation includes:
- PR #5 baseline and packaging/reliability work.
- PR #9 lifecycle resource ownership / CA1001 remediation.
- PR #11 and PR #25 listening-metadata privacy redaction.
- PR #12 loved-track and artwork fallback UI.
- PR #16 local-day/DST boundary correction.
- PR #18 browser-origin authorization.
- PR #19 hamburger navigation and functional collapsed icon rail with tooltips.
- PR #21 tag-driven preview release workflow.
- PR #23 rejection of unknown or inconsistent database migration histories.
- PR #28 fail-closed SQLite restore replacement with WAL/SHM rollback and Windows regression coverage.

PR #28 was merged as commit `0436ed83762971ef75dae5be1ffd15bab7e1eae5`. Its exact-head Windows CI passed: [run 39785201272](https://github.com/frpboy/Scrobblix/actions/runs/39785201272). The post-merge CI run for the resulting `main` SHA was still in progress when this document was edited.

## Current release status

**Not yet qualified for public/stable release.** A preview release exists, but it is unsigned and is not a production installer.

- Preview: [Scrobblix v0.1.0-beta.1](https://github.com/frpboy/Scrobblix/releases/tag/v0.1.0-beta.1)
- Release automation: [workflow implementation](https://github.com/frpboy/Scrobblix/commit/7823af1e5be0deeae38ad6f08b5404d3d45d7451)
- Preview downloads include a Windows test bundle, browser companion bundle, and SHA-256 checksum file.
- The preview workflow uses CI/package validation; it does not establish real-device qualification, signing, installer upgrade/rollback, browser-store publication, or stable release readiness.

## Active release blocker — issue #17

[Issue #17: real-world qualification matrix](https://github.com/frpboy/Scrobblix/issues/17) remains open. The following checks must remain **NOT RUN** or **BLOCKED** until someone executes them in the required environment and records evidence.

### Last.fm and listening pipeline
- [ ] Use a dedicated Last.fm test account/application; confirm accepted scrobbles appear on the test profile.
- [ ] Verify provider response reconciliation: mark delivered only after acceptance; preserve ambiguous outcomes for safe retry.
- [ ] Test duplicate event delivery, repeated tracks, identical metadata from multiple sources, pause/resume/seek, late metadata, duration changes, and restart mid-queue.
- [ ] Test queue lease expiration, bounded retry/backoff, crash recovery, throttling, and revoked sessions.
- [ ] Run a multi-hour soak with redacted correlation IDs and no production listening history.

### Installed browser integration
- [ ] Install/load Chromium companion in Chrome and Edge separately and record actual extension IDs.
- [ ] Verify native messaging registration, restart/reconnect, playback state changes, hostile/malformed/oversized payloads, allow-list rejection, stdout discipline, and uninstall cleanup.
- [ ] Test Firefox temporary add-on separately; do not imply persistent signed distribution.

### Real Windows desktop and accessibility
- [ ] Test tray Open/Exit, minimize/restore, Explorer restart recovery, notification opt-in/activation/privacy, duplicate launch and relaunch.
- [ ] Test keyboard/focus order, Narrator, contrast, high DPI/scaling, reduced motion, and window resizing.
- [ ] Verify single-instance IPC identity assumptions, named-pipe access, inbound message validation, and rate limits.

### Installation and distribution
- [ ] Test clean install, upgrade, failed update recovery, rollback, uninstall, and retained-data choices on a clean Windows environment.
- [ ] Produce a signed MSIX/App Installer or complete the intended Store distribution identity.
- [ ] Verify signature/timestamp, publisher identity, update path, dependency licenses, SBOM, and artifact provenance.
- [ ] Exercise backup/restore corruption, checksum mismatch, interrupted restore, schema compatibility, and post-restore startup.

### UI design handoff
- [ ] Import `Scrobblix-Docs/04_UserExperience/STITCH_IMPORT_BRIEF.md` into the real Stitch workspace only when the integration is available.
- [ ] Verify the real editable Home, History, Queue, Sources, Diagnostics, and Settings frames against WinUI controls and design tokens.
- [ ] A checked-in brief is not proof that the external Stitch workspace changed. Record the workspace link only after verifying the actual project.

## Validation evidence rules

- Exact-head CI for PR #28 passed at [run 39785201272](https://github.com/frpboy/Scrobblix/actions/runs/39785201272).
- Post-merge CI for main SHA `0436ed83762971ef75dae5be1ffd15bab7e1eae5` was **IN PROGRESS** at the time of this edit; re-check [run 39812019297](https://github.com/frpboy/Scrobblix/actions/runs/39812019297).
- CI proves only what that workflow executes on that commit and runner. It does not prove live Last.fm acceptance, actual browser installation/event delivery, GUI tray recovery, accessibility, signing, installation/upgrade/rollback, or clean-machine behavior.
- Never mark a manual case PASS without executing it and recording the candidate SHA, environment, result, and sanitized evidence.
- Never place Last.fm API secrets/session keys or raw listening metadata in source control, logs, screenshots, or issue comments.

## Next actions

1. Confirm the post-merge CI run for current `main` SHA `0436ed83762971ef75dae5be1ffd15bab7e1eae5` succeeds.
2. Execute the issue #17 real-world matrix on a dedicated test account and real Windows/browser installations; keep unexecuted cases NOT RUN/BLOCKED.
3. Make a documented private-beta go/no-go decision tied to the exact candidate SHA, CI run, completed matrix, and distribution/signing state.
