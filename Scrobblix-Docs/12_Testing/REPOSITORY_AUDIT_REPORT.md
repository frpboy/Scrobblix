# Scrobblix Repository Audit Report

> **Audit date:** 2026-10-23  
> **Repository:** https://github.com/frpboy/Scrobblix  
> **Audit-start base SHA:** `0436ed83762971ef75dae5be1ffd15bab7e1eae5`  
> **Rebased integration base:** `main` / `e0b99774bb62bcc2ab160a8e98b24051f382641b`  
> **Audit branch:** `docs/reconcile-release-status-2026-10-23`  
> **Status:** Prior source fixes are merged; this report is being reconciled to the current main SHA.  
> **Release recommendation:** **Not ready for private beta** until the real-world qualification matrix is executed and all blocking outcomes are resolved.

## Executive summary

The GitHub tree at the audit baseline contained 401 tracked files (tree API reported no truncation): 222 files under `Scrobblix-Docs`, 114 under `src`, 17 under `docs`, 12 under `browser-extension`, 9 under `scripts`, 2 under `tests`, 2 under `skills`, and 21 root files. Of those, 133 tracked files matched implementation/configuration extensions used to map the C#, XAML, TypeScript/JavaScript, SQL, PowerShell, project, JSON and workflow surface. This count is an inventory aid, not a claim that every individual line received a human review.

The audit reconciled current issues and PRs, release prerequisites, the real-world matrix, migration/restore hardening, browser/native-host boundaries, playback ingestion/tracking, provider delivery, metadata/artwork fetching, diagnostics/privacy, CSV, lifecycle, release workflows and helper scripts. The GitHub tree currently has 405 tracked files. Current main has additionally merged PR #33 (queue account isolation/artwork connection pinning), PR #35 (CSV formula-prefix hardening), and PR #37 (provider-action lifecycle outcomes).

Confirmed and closed code-defect findings include #10 (P2 privacy-safe logging), #13 (P2 daylight-saving day boundaries), #15 (P2 browser-origin registration), #30 (P1 account/provider queue isolation), #32 (P2 artwork destination connection pinning), #34 (P2 CSV formula prefixes) and #36 (P3 lifecycle completion-state consistency). Their fixes were merged in PRs #11/#25, #16, #18, #33, #35 and #37 respectively, with exact-head Windows CI evidence linked in the validation section below. The mainline CI at the current baseline also passed.

## Baseline and environment

| Item | Verified value |
|---|---|
| Default branch | `main` |
| Audit-start SHA | `0436ed83762971ef75dae5be1ffd15bab7e1eae5` |
| Current integration base | `e0b99774bb62bcc2ab160a8e98b24051f382641b` |
| Baseline workflow | [Windows CI run 39812019297](https://github.com/frpboy/Scrobblix/actions/runs/39812019297) |
| Baseline workflow result | Passed for that exact baseline SHA |
| Current main workflow | [Run 39901957156](https://github.com/frpboy/Scrobblix/actions/runs/39901957156) passed on `e0b99774bb62bcc2ab160a8e98b24051f382641b` |
| Tracked-file inventory | 401 files at audit start; 405 files on current main; Git tree response not truncated |
| Latest published release at baseline | [v0.1.0-beta.1](https://github.com/frpboy/Scrobblix/releases/tag/v0.1.0-beta.1), prerelease |
| Real-world qualification | The repo's manual matrix says real-machine, provider, installed-browser, UX and installer scenarios remain `NOT RUN` |
| Local validation limitation | Clone attempt failed because the environment could not resolve github.com; .NET SDK and PowerShell were not present locally. Node.js v22.16.0 was present. |
| Repository policy visibility | Branch-protection endpoint returned HTTP 403 to the connected integration; protection settings were not changed. |

The current main CI run validates the integrated changes at SHA `e0b99774bb62bcc2ab160a8e98b24051f382641b` for the automated stages configured in Windows CI. It does not validate real-world provider, installed-browser, live Windows UI or installer behavior.

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
| AUD-QUEUE-001 | Queue lease did not scope provider/account although the worker supplied the connected account's session key | P1 | [#30](https://github.com/frpboy/Scrobblix/issues/30) | Closed; merged PR [#33](https://github.com/frpboy/Scrobblix/pull/33), two-account/provider regression coverage |
| AUD-SEC-002 | Artwork hostnames were not validated/pinned to the addresses used for outbound connections | P2 | [#32](https://github.com/frpboy/Scrobblix/issues/32) | Closed; merged PR [#33](https://github.com/frpboy/Scrobblix/pull/33), DNS/address and resolver regression coverage |
| AUD-CSV-001 | CSV export omitted LF and full-width spreadsheet formula prefixes | P2 | [#34](https://github.com/frpboy/Scrobblix/issues/34) | Closed; merged PR [#35](https://github.com/frpboy/Scrobblix/pull/35) with regression coverage |
| AUD-QUEUE-002 | Domain lifecycle helper rejected valid persisted terminal outcomes | P3 | [#36](https://github.com/frpboy/Scrobblix/issues/36) | Closed; merged PR [#37](https://github.com/frpboy/Scrobblix/pull/37), lease cleanup tests |
| AUD-REL-001 | Real-world Windows/provider/browser/recovery/distribution qualification not executed | Release blocker | [#17](https://github.com/frpboy/Scrobblix/issues/17) | Open intentionally; the matrix remains NOT RUN until tests actually execute |
| AUD-DOC-001 | Release status and continuation checkpoint stale at baseline | P2/documentation | [#27](https://github.com/frpboy/Scrobblix/issues/27) | Current refresh is in this docs PR; close only after it merges and the current-main evidence is rechecked |

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
