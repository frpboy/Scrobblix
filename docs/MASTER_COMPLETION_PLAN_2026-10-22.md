# Scrobblix — Master Completion Plan (2026-10-22)

> Status: implementation exists; private beta is **NO-GO** until real-world P0 gates are evidenced.
> Integration base: `main`. Active continuation: `feat/private-beta-readiness`. This plan supplements—not replaces—the detailed requirements, release checklist and manual matrix.

## 1. Current state and evidence

- Main product surfaces exist: Home, History, Statistics, Loved, Queue, Sources, Diagnostics and Settings.
- Implemented code paths include GSMTC playback observation, optional browser companion/native messaging, playback identity/source arbitration, SQLite history and queue persistence, Last.fm auth/actions, metadata/artwork enrichment, local backup/restore, tray/notifications, diagnostics and a privacy-reduced support bundle.
- The private-beta readiness line includes pipe-warning backoff/throttling, a local build guard, support-bundle canary checks, browser payload/origin validation, source-state clarity and Home status assertions.
- Matching candidate CI reported success for SHA `00a290e32eb9dc97d1cc5d7c4440bb54374e9e02` / run [39748050490](https://github.com/frpboy/Scrobblix/actions/runs/39748050490). This validates the automated pipeline at that SHA only. Recheck the latest head/run before any merge or release.
- PR [#7](https://github.com/frpboy/Scrobblix/pull/7) is the current private-beta readiness draft. Older PR #5 and its CA1001 failure checkpoint are historical, not proof of current branch failure.
- Live Windows/Spotify/Last.fm/browser tests and signed installer qualification remain unproven. Keep corresponding matrix entries `NOT RUN` until actually tested.

## 2. Execution order

### P0 — Stability, real scrobble correctness, durable recovery
1. Fetch the newest `main` and `feat/private-beta-readiness` refs; review divergence and validate the exact candidate SHA.
2. Build and launch on Windows with clean startup, normal close, close/minimize-to-tray, reopen, duplicate instance and app restart scenarios. If any unexpected exit remains, record process exit code, Windows Application event, and timestamp-aligned sanitized logs; do not infer root cause from pipe warnings alone.
3. Reproduce native-host pipe contention, confirm bounded exponential retry and 30-second warning throttling, check suppressed-warning counters, then verify reconnection.
4. Run a dedicated Last.fm test account across multiple days. Cover eligibility threshold, pause/resume, seeks, skips, short tracks, repeated same track after an intervening track, ad playback, metadata arriving late, source overlap, network loss, auth expiry, timeout after sending, and process restart.
5. Reconcile every qualifying local play to the matching provider-accepted action and the dedicated Last.fm profile. Distinguish accepted, pending, retry scheduled, auth blocked and outcome unknown. No unexplained missing or duplicate scrobbles is acceptable.
6. Test queued-action lease expiry, crash/restart recovery, offline backlog drain, migrations and backup/restore using disposable test data. Test corrupt archives, checksum/schema mismatch and interrupted restore. Run an 8–24 hour soak and record CPU/memory, SQLite/log growth, queue state and any tracking gap.

**P0 exit rule:** zero unexplained qualifying-play loss/duplication, stable restart/recovery, and evidence attached to the exact tested candidate. Any mismatch keeps beta **NO-GO**.

### P1 — Browser companion (or exclude it from beta claims)
1. Install Chrome and Edge separately; record their real, distinct extension IDs. Verify Native Messaging registration and allow-lists.
2. Test supported pages through play/pause/seek/skip/end, tab reload, browser restart, native-host restart and reconnect.
3. Load the Firefox development add-on and verify its actual identity and event delivery. Do not describe a temporary add-on as a persistent signed release.
4. Verify bad origins/IDs, forged ping, malformed/oversized payload, invalid types, non-HTTPS artwork and uninstall cleanup are rejected safely.
5. If any browser path cannot be tested, state explicitly that companion support is experimental/omitted; desktop GSMTC must continue to work without it.

### P1 — Windows UX, accessibility, diagnostics and security
1. Review Home, Sources, Queue, Diagnostics and Settings on actual Windows at 100%, 150% and 200% scaling; confirm no clipping and clear empty/loading/error/paused/eligible/pending/retry/accepted states.
2. Run keyboard-only navigation, Narrator, high-contrast theme, focus order and notification privacy checks.
3. Test tray icon restoration after Explorer restart; minimize/restore/Open/Exit; notification activation/expiry; clean shutdown and duplicate launch.
4. Inspect support bundle ZIP contents and checksum in the app. Seed test-only canary values and assert none survive in any bundle entry. Confirm Cancel creates nothing and there is no auto-upload.
5. Complete a focused same-user named-pipe/native-host threat review: peer path/image checks are defense-in-depth, not cryptographic process identity. Review message bounds, allow-list changes, sequence/replay handling, permissions and error/log amplification.

### P1 — Reproducible Windows release
1. Decide the release channel: signed MSIX/App Installer or Microsoft Store.
2. Complete publisher identity, signing certificate/store identity, timestamp, dependency/license inventory, SBOM and provenance.
3. Test clean install on a clean Windows profile/device, upgrade, repair, failed update, rollback, uninstall, retained-data choice and runtime prerequisite handling.
4. Publish checksums, versioned release notes, known issues, supported Windows/runtime matrix, and a clearly labelled test build while unsigned.

### P2 — After v1 reliability
- Improve installer/update UX only after install/upgrade/rollback behavior is tested.
- Localization and broader platform ideas remain deferred until Windows reliability targets are met.
- Do not add cloud sync, recommendation engines, audio fingerprinting, social features or a second client platform as part of v1 completion.

## 3. Evidence and change discipline

- CI green means compile/assertion/browser/package jobs passed for a specific SHA. It does not prove live provider acceptance, real browser events, actual WinUI accessibility, crash recovery, signing or rollback.
- A checkbox can be marked complete only with: tested commit SHA, environment/app/browser versions, date, outcome, redacted evidence link, and defects created.
- Every behavior-changing code patch must update relevant tests and documentation. Do not suppress analyzer rules merely to green CI.
- Keep listening history, API keys, shared secrets, Last.fm session keys, raw crash dumps and full unredacted logs out of commits, artifacts shared publicly, and issue comments.
- The Stitch handoff and exported screenshots are design references; only claim a Stitch workspace edit when the actual external workspace revision/link is available.

## 4. Commands for the owner on Windows

Close Scrobblix before build. The helper must not silently kill a running instance.

```powershell
git status --short
git fetch origin --prune
git switch feat/private-beta-readiness
git pull --ff-only origin feat/private-beta-readiness
.\scripts\update-and-run.ps1
```

For targeted validation:

```powershell
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
```

Record actual manual outcomes in [REAL_WORLD_MANUAL_TEST_MATRIX.md](../Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md). Do not change `NOT RUN` to passed based on expectations or CI alone.
