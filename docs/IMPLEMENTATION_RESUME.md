# Scrobblix implementation resume guide

Repository: frpboy/Scrobblix  
Working branch: feat/scrobblix-v1-complete  
Review: https://github.com/frpboy/Scrobblix/pull/4  
Last green baseline before docs/tooling hardening: [CI run 39718022389](https://github.com/frpboy/Scrobblix/actions/runs/39718022389), commit 4f0ab63ebccdc445caed5d0ea5410c14f89a9cba.

## Current verified position

Owner screenshots/video show playback detection, Last.fm connected status, local Today/Queue counts, Settings, Sources, Queue, Diagnostics and some fetched artwork. A Last.fm profile screenshot shows scrobbles appearing. This is useful end-to-end evidence, but accepted-action/profile reconciliation, repeated-track dedup, real browser-native installation, tray/notification lifecycle and signed release testing remain open.

The last green CI baseline built the .NET solution with analyzers, ran domain/SQLite assertions, checked and built the browser companion, validated Chromium/Firefox manifests and native-host hooks, published the unpackaged app/native host, and assembled a Windows test bundle. CI validates only the steps it executed—not a production release or live Windows/browser certification.

## Current implementation snapshot

- GSMTC playback, canonical tracking, confirmed-listening eligibility, source arbitration, late-metadata-tolerant identity, local history and Today summary.
- SQLite queue idempotency, leases, bounded retry/backoff, restart recovery, provider states, Last.fm scrobble/Now Playing/Love/Unlove.
- Last.fm desktop auth, Credential Locker integration, API signatures and account status.
- MusicBrainz/Cover Art Archive/Last.fm metadata and art fallback, local cache and no-art placeholder.
- Home, History, Statistics, Loved, Queue, Sources, Diagnostics and Settings; numbered UTC logs.
- SQLite backup validation, staged restore and pre-restore safety snapshot.
- Tray/minimize prototype and opt-in notifications with track details hidden by default.
- Chromium/Firefox development manifests and Native Messaging registration.
- Windows CI, lock-aware run helper, Chromium MV3 validator and bundle relative-path checks.

## Continue from the current branch

At the existing clone path (the owner uses E:\K4NN4N\Scrobblix), close the running app before rebuilding. Then run:

    git status --short
    git fetch origin --prune
    git switch feat/scrobblix-v1-complete
    git pull --ff-only origin feat/scrobblix-v1-complete
    .\scripts\update-and-run.ps1

To explicitly let the helper request shutdown before rebuilding:

    .\scripts\update-and-run.ps1 -StopRunningApp

That option is opt-in. The helper requests a normal close first and force-stops only leftover processes. By default, a running app results in a clear error before build.

For validation without launching:

    dotnet restore Scrobblix.slnx
    dotnet build Scrobblix.slnx --no-restore --configuration Release
    dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release --no-build
    npm --prefix browser-extension ci
    npm --prefix browser-extension run check
    npm --prefix browser-extension run build
    node scripts/validate-browser-manifests.mjs

See docs/LOCAL_DEVELOPMENT_TROUBLESHOOTING.md for the observed lock error.

## Remaining work

1. P0 — replace any exposed credentials, save replacements in Credential Locker, reconcile accepted actions with Last.fm history and repeat same-track/restart testing without duplicates.
2. P0 — install browser companion in Chrome/Edge and temporary Firefox, register actual IDs, verify events and uninstaller.
3. P0 — test Windows tray minimize/restore/Exit, Explorer restart recovery, notification privacy/activation, DPI/high contrast and clean shutdown.
4. P1 — signed MSIX/App Installer, upgrade/rollback/uninstall, clean-machine prerequisites, signature/provenance/SBOM/license review.
5. P1 — same-user IPC review, retention/delete/export, backup migrations, log rotation and support-bundle redaction.
6. P1 — long-run tests, repeated tracks, pause/seek/ad transitions, late/missing metadata, network loss, revoked sessions, throttling, time-zone edges and accessibility.
7. Stitch — a detailed Git handoff exists, but Stitch MCP was unavailable. Do not claim the external workspace was updated until the connector and project are verified.

Continue on feat/scrobblix-v1-complete and PR #4; do not reset main or merge only because CI is green. Check the latest SHA and CI before distributing.
