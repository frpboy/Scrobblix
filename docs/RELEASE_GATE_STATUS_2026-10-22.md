> **Historical release-gate snapshot — not current status.** This document was prepared on 2026-10-23 and references earlier main commits/workflows. Do not use its SHA, PR/issue states, or CI results as current evidence. For current implementation status, start at [README](../README.md) and [Implementation Resume](IMPLEMENTATION_RESUME.md); for manual gates, use [Private Beta Readiness](PRIVATE_BETA_READINESS.md) and the [real-world manual test matrix](../Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md).

# Scrobblix v1 — implementation and release status

Updated: 2026-10-23 (UTC)
Repository: https://github.com/frpboy/Scrobblix
Default branch: `main`
Current main SHA at this checkpoint: `d27d06d809d59b1ca17e81a29019fc44be700b90`
Current main CI: [run 39903649228](https://github.com/frpboy/Scrobblix/actions/runs/39903649228) — **PASSED on exact SHA `d27d06d809d59b1ca17e81a29019fc44be700b90`**.

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
- PR #31 preservation of enriched artwork across repeated playback polls.
- PR #33 provider/account-scoped queue leasing and DNS-pinned public-address artwork connections; issues [#30](https://github.com/frpboy/Scrobblix/issues/30) and [#32](https://github.com/frpboy/Scrobblix/issues/32) are closed.
- PR #35 CSV formula-prefix neutralization for LF and full-width formula characters; issue [#34](https://github.com/frpboy/Scrobblix/issues/34) is closed.
- PR #37 provider-action lifecycle support for `SubmissionOutcomeUnknown` and `RetryExhausted`; issue [#36](https://github.com/frpboy/Scrobblix/issues/36) is closed.
- PR #40 stops confirmed-time accrual across a sustained missing GSMTC session and preserves browser-owned playback; issue [#39](https://github.com/frpboy/Scrobblix/issues/39) is closed. Exact-head Windows CI passed in [run 39903263712](https://github.com/frpboy/Scrobblix/actions/runs/39903263712).

PR #28 merged with exact-head Windows CI passing in [run 39785201272](https://github.com/frpboy/Scrobblix/actions/runs/39785201272). Subsequent relevant fixes include PR #31 (artwork retention), PR #33 (provider/account isolation and safe artwork connections; exact-head CI [run 39895414307](https://github.com/frpboy/Scrobblix/actions/runs/39895414307) on SHA `56609db5f5076695c5da9f3c9288b940faecab2c`), PR #35 (CSV formula-prefix neutralization; exact-head CI [run 39896613043](https://github.com/frpboy/Scrobblix/actions/runs/39896613043) on SHA `94038c83a304b3ba84f31123ab01f586bea809e6`), and PR #37 (provider-action lifecycle outcomes; exact-head CI [run 39901751712](https://github.com/frpboy/Scrobblix/actions/runs/39901751712) on SHA `4428685963a695c35be543128ec7edb31b2535af`). Current `main` SHA `e0b99774bb62bcc2ab160a8e98b24051f382641b` passed integrated Windows CI in [run 39901957156](https://github.com/frpboy/Scrobblix/actions/runs/39901957156).

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
- Exact-head CI for PRs #33, #35, #37 and #40 passed at runs 39895414307, 39896613043, 39901751712 and 39903263712 respectively.
- Previous main SHA `e0b99774bb62bcc2ab160a8e98b24051f382641b` passed integrated Windows CI in run 39901957156. The current main SHA `d27d06d809d59b1ca17e81a29019fc44be700b90` passed its integrated post-merge workflow in [run 39903649228](https://github.com/frpboy/Scrobblix/actions/runs/39903649228).
- CI proves only what that workflow executes on that commit and runner. It does not prove live Last.fm acceptance, actual browser installation/event delivery, GUI tray recovery, accessibility, signing, installation/upgrade/rollback, or clean-machine behavior.
- Never mark a manual case PASS without executing it and recording the candidate SHA, environment, result, and sanitized evidence.
- Never place Last.fm API secrets/session keys or raw listening metadata in source control, logs, screenshots, or issue comments.

## Next actions

1. Recheck current `main` and its latest exact-head Windows CI before using it as a candidate; `d27d06d809d59b1ca17e81a29019fc44be700b90` passed post-merge validation in run 39903649228. Issue #27 tracks the current audit/checkpoint refresh and should close only after the documentation PR merges.
2. Execute the issue #17 real-world matrix on a dedicated test account and real Windows/browser installations; keep unexecuted cases NOT RUN/BLOCKED.
3. Make a documented private-beta go/no-go decision tied to the exact candidate SHA, CI run, completed matrix, and distribution/signing state.
