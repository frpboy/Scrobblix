# Scrobblix Repository Audit Report

> **Audit date:** 2026-10-23  
> **Repository:** https://github.com/frpboy/Scrobblix  
> **Audit-start base SHA:** `0436ed83762971ef75dae5be1ffd15bab7e1eae5`  
> **Rebased integration base:** `main` / `d27d06d809d59b1ca17e81a29019fc44be700b90`  
> **Audit branch:** `docs/reconcile-current-main-final`  
> **Status:** Source fixes through PR #40 are merged; audit and release docs are being reconciled to current main.  
> **Release recommendation:** **Not ready for private beta** until the real-world qualification matrix is executed and all blocking outcomes are resolved.

## Executive summary

The GitHub tree at the audit baseline contained 401 tracked files (tree API reported no truncation): 222 files under `Scrobblix-Docs`, 114 under `src`, 17 under `docs`, 12 under `browser-extension`, 9 under `scripts`, 2 under `tests`, 2 under `skills`, and 21 root files. Of those, 133 tracked files matched implementation/configuration extensions used to map the C#, XAML, TypeScript/JavaScript, SQL, PowerShell, project, JSON and workflow surface. This count is an inventory aid, not a claim that every individual line received a human review.

The audit reconciled current issues and PRs, the release prerequisites, the real-world test matrix, the migration and restore hardening already merged, the browser/native-host boundary, playback ingestion and tracking, provider queue delivery, metadata/artwork fetching, diagnostics/privacy, CSV and backup boundaries, lifecycle, release workflows and helper scripts. During the audit the main branch advanced through PR #31; the working branch was restarted from the new main SHA rather than rewritten.

Three earlier defect issues are already closed through merged fixes on the baseline: diagnostic-log privacy (#10), daylight-saving “Today” bounds (#13), and explicit browser-origin registration (#15). During this deeper pass, two additional code-level findings were confirmed and documented:
- **#30 (P1):** provider actions were claimable without filtering by account/provider, while the worker supplied the currently connected account's session key. This could submit one account's queued scrobble or Love/Unlove action under another account.
- **#32 (P2):** artwork URL validation rejected literal private addresses but did not validate/pin DNS-resolved destinations, leaving a hostname/redirect path that could resolve into a private or special-use network range.

Fixes and regression tests for #30 and #32 are committed to this audit branch. **These new changes are not yet validated by a completed exact-head CI run at the time of this report.** They must remain open until PR review and CI establish success.

## Baseline and environment

| Item | Verified value |
|---|---|
| Default branch | `main` |
| Audit-start SHA | `0436ed83762971ef75dae5be1ffd15bab7e1eae5` |
| Refreshed integration base | `609d418580cb902d0ba14e949b9b41d756616932` |
| Baseline workflow | [Windows CI run 39812019297](https://github.com/frpboy/Scrobblix/actions/runs/39812019297) |
| Baseline workflow result | Passed for that exact baseline SHA |
| Post-PR #31 main workflow | [Run 39894119316](https://github.com/frpboy/Scrobblix/actions/runs/39894119316) passed on `609d418580cb902d0ba14e949b9b41d756616932` |
| Tracked-file inventory | 401 files; Git tree response not truncated |
| Latest published release at baseline | [v0.1.0-beta.1](https://github.com/frpboy/Scrobblix/releases/tag/v0.1.0-beta.1), prerelease |
| Real-world qualification | The repo's manual matrix says real-machine, provider, installed-browser, UX and installer scenarios remain `NOT RUN` |
| Local validation limitation | Clone attempt failed because the environment could not resolve github.com; .NET SDK and PowerShell were not present locally. Node.js v22.16.0 was present. |
| Repository policy visibility | Branch-protection endpoint returned HTTP 403 to the connected integration; protection settings were not changed. |

The baseline CI result validates only the baseline commit. It does not validate the audit-branch queue/account-isolation and safe-artwork-connection changes. The exact branch head must be validated through its pull-request workflow.

## Component map and end-to-end data flow

### Playback and eligibility

- Windows GSMTC and browser content scripts collect observations and report playback metadata/state.
- Browser observations pass through extension schema/origin validation, the native-messaging host allowlist, bounded framed input, and the desktop browser-ingress pipe/peer checks.
- `PlaybackObservationValidator`, `PlaybackObservationIngestionService`, and `SqlitePlaybackObservationStore` validate and persist source observations with duplicate protection.
- `PlaybackSourceArbitrator` selects/merges competing GSMTC and browser events; `PlaybackTrackingService` and `CanonicalPlayTracker` manage the canonical play, confirmed elapsed time, and eligibility.
- `ScrobbleEligibility` applies the documented rules: a usable artist/title, valid duration above 30 seconds, and confirmed playback time reaching the lesser of half the duration or four minutes, with exclusions for explicitly disqualified/duplicate states.
- Eligible local plays are persisted and provider actions are queued transactionally; provider delivery is performed separately by the queue worker.

### Provider delivery and account boundaries

- Last.fm API credentials and account session keys are held through the configured Windows Credential Locker integration; the browser companion never receives the session key.
- `ProviderActionProcessor` converts durable leased actions into scrobble, now-playing, Love, and Unlove requests, records provider outcomes, applies retry/backoff rules, and leaves ambiguous outcomes for reconciliation rather than blindly retrying.
- **Audit fix:** `IProviderActionStore.ClaimNextDueAsync`, `SqliteProviderActionStore`, the processor, and the desktop queue worker now pass/filter both provider and account identity. A worker cannot claim another account's older due action.

### Metadata and artwork

- `TrackMetadataEnricher` queries MusicBrainz and Cover Art Archive, optionally falls back to Last.fm, merges enriched fields, and caches artwork under a content-keyed local filename.
- Artwork retrieval uses HTTPS-only URI checks, explicit redirect handling, image content-type validation, an 8 MiB streamed cap and a dedicated client.
- **Audit fix:** the dedicated artwork client now validates resolved destination IPs in its connection callback and connects to the specific validated address, rejecting non-public answers and IPv4-mapped private addresses before opening the socket. It does not use the system proxy for artwork because a proxy could resolve the destination independently; this trade-off should be considered for managed/corporate environments.

### Persistence, recovery and diagnostics

- SQLite migrations are embedded, checksummed, applied transactionally, and the database refuses unknown or non-prefix migration history rather than silently downgrading.
- History, queue, statistics, Love/Unlove intents and local settings have dedicated storage/read-model paths.
- Backup restore validates the archive allowlist, version/manifest, size, SHA-256, SQLite integrity/schema and stages before replacing the active database; the latest merged restore fix preserves and restores the SQLite/WAL/SHM rollback set on swap failure.
- The support bundle exports a diagnostics allowlist without opening the listening database, preferences, Credential Locker, artwork directory, or raw crash dumps. The central logger redacts credentials, authorization URLs and listening metadata.

### Packaging and release

- Windows CI restores/builds .NET, validates desktop contracts, runs the domain/Infrastructure assertion program, type-checks and builds the browser companion, tests message/origin validation, validates manifests, parses PowerShell helpers, publishes the unpackaged desktop and native host, and assembles/uploads test artifacts.
- Tag-triggered release automation publishes an explicitly labelled unsigned preview with SHA-256 checksums; that artifact is not a signed production installer and is not proof of browser-store publication or Windows upgrade qualification.

## Confirmed findings and issue reconciliation

| Audit ID | Finding | Severity | Canonical issue | Fix / current status |
|---|---|---|---|---|
| AUD-PRIV-001 | Listening artist/title/album values in diagnostic logs | P2 | [#10](https://github.com/frpboy/Scrobblix/issues/10) | Closed; merged PR [#11](https://github.com/frpboy/Scrobblix/pull/11), further delimiter-safe handling via merged PR #25 |
| AUD-DATE-001 | “Today” summary assumed an elapsed 24-hour day at DST boundaries | P2 | [#13](https://github.com/frpboy/Scrobblix/issues/13) | Closed; merged PR [#16](https://github.com/frpboy/Scrobblix/pull/16), 23/25-hour regression cases |
| AUD-BROWSER-001 | Broad optional host permission could be treated as sufficient custom-origin authorization | P2 | [#15](https://github.com/frpboy/Scrobblix/issues/15) | Closed; merged PR [#18](https://github.com/frpboy/Scrobblix/pull/18), explicit origin-registration binding |
| AUD-QUEUE-001 | Queue lease did not scope provider/account although the worker supplied the current account's session key | P1 | [#30](https://github.com/frpboy/Scrobblix/issues/30) | Fix and two-account/provider regression coverage on this branch; CI/review pending |
| AUD-SEC-002 | Artwork hostnames were not validated/pinned to the addresses used for outbound connections | P2 | [#32](https://github.com/frpboy/Scrobblix/issues/32) | Dedicated socket connector and resolver regressions on this branch; CI/review pending |
| AUD-REL-001 | Real-world Windows/provider/browser/recovery/distribution qualification not executed | Release blocker | [#17](https://github.com/frpboy/Scrobblix/issues/17) | Open intentionally; the matrix remains NOT RUN until tests actually execute |
| AUD-DOC-001 | Release status and continuation checkpoint stale at baseline | P2/documentation | [#27](https://github.com/frpboy/Scrobblix/issues/27), [PR #29](https://github.com/frpboy/Scrobblix/pull/29) | PR #29 was open during this audit and addresses current-main status/checkpoint; preserve its review/CI evidence and avoid duplicating it |

The register at `REPOSITORY_AUDIT_REGISTER.json` contains the stable audit IDs, impact, issue links, fix mapping and validation state. Superseded PRs #8 and #14 are not treated as active fixes; current merged state is taken from live GitHub, not stale checkpoint prose.

## Validation and evidence

### Verified on the baseline SHA

Windows CI run [39812019297](https://github.com/frpboy/Scrobblix/actions/runs/39812019297) succeeded on `0436ed83762971ef75dae5be1ffd15bab7e1eae5`. That workflow includes the .NET build, desktop UI contract checks, domain/Infrastructure assertions, browser TypeScript/build/security/manifest checks, PowerShell parsing, Windows/native-host publish and artifact assembly/upload.

### Required for this audit branch

The following must run on the **exact final audit PR head** after all branch changes:
- Windows .NET Release build with warnings-as-errors.
- Desktop UI contract script and the full domain/Infrastructure assertion executable.
- Account/provider isolation tests against a temporary SQLite database.
- Public/private IPv4/IPv6 and IPv4-mapped address checks plus a controlled DNS resolver test that proves rejection occurs before a socket is opened.
- Browser TypeScript/build/security assertions and both manifest validators.
- PowerShell parser, Windows publish, NativeHost publish, artifact assembly and upload.
- Full diff and secret/artifact review.

No passing result is reported for these new changes until their exact-head run is complete. No local command result is claimed: the local environment could not clone from GitHub, and .NET/PowerShell were unavailable.

## Real-world qualification remains outstanding

`Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md` remains the authoritative manual record. It explicitly keeps installed-browser, Last.fm reconciliation, long-run playback, tray/accessibility, restore/upgrade/rollback/uninstall and private-beta gate rows at `NOT RUN`.

Minimum required qualification before private beta:
1. A dedicated Last.fm test account: reconcile eligible local plays to accepted remote scrobbles, and test ambiguous timeouts, retries, duplicate events, account switching, Love/Unlove and restart recovery.
2. Actual Chrome/Edge installations (Firefox only within its supported install model): extension identity, native-host registration, supported and custom origin behavior, restart/reconnect and uninstall cleanup.
3. Real Windows user session: tray, notifications, Explorer restart, keyboard/Narrator, display scaling, artwork decode/fallback, shutdown/relaunch and single-instance behavior.
4. Backup and restore on disposable data, including corrupt archive, checksum mismatch, interrupted restore, sidecar swap failure and startup after restore.
5. Signed production packaging, publisher identity, update trust and install/upgrade/rollback/uninstall, when a production installer exists.

Record `PASS`, `FAIL`, `BLOCKED`, or `NOT RUN` only according to the test actually executed. Do not use the preview ZIP as evidence of stable release readiness.

## Current remaining risks

- The account/provider scoping fix (#30) and the connection-pinned artwork fix (#32) need exact-head CI and review before merge.
- The artwork UI correction merged as [PR #31](https://github.com/frpboy/Scrobblix/pull/31) at main commit `609d418580cb902d0ba14e949b9b41d756616932`; its merge has its own passing workflow, and its MainWindow changes were preserved when the audit branch was restarted.
- The release-checkpoint refresh PR [#29](https://github.com/frpboy/Scrobblix/pull/29) was open at audit time. Track its review comments, including markdown formatting, and do not overwrite that branch.
- The real Windows/provider/manual matrix is not executed in this environment, and branch-protection details are not available through the current GitHub integration.

## Release recommendation

The baseline source and CI were in a good automated state, but **this audit candidate is not ready to merge until exact-head CI passes**, and **Scrobblix is not ready for private beta until issue #17's real-world release gates have been executed and resolved**.

See `REPOSITORY_AUDIT_CHECKPOINT.md` for the exact continuation state and next actions.
