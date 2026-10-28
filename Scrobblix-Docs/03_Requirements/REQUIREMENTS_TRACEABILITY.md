# Requirements Traceability — Current Implementation Baseline

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Last reviewed against source:** 2026-10-28  
> **Status:** Starter traceability; manual release evidence remains incomplete

## How to read this matrix

A code reference means the behavior exists in the current source tree; it does not mean every scenario is qualified. CI results must be tied to the exact candidate SHA. Real Windows, installed-browser, live Last.fm, UI accessibility and endurance scenarios remain manual and must stay `NOT RUN` until evidence is recorded in [the real-world test matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md).

Automated coverage currently includes the domain/infrastructure assertions in `tests/Scrobblix.Domain.Tests`, desktop source-level UI contracts, browser sender/payload checks, manifest validation, release workflow wiring, and documentation path/structure validation. These do not constitute a browser E2E suite, live provider qualification, full vulnerability/license/SBOM audit, or accessibility certification.

## Implementation and evidence map

| Requirement group | Current code / component | Automated evidence in repository | Outstanding acceptance evidence |
|---|---|---|---|
| FR-A — app lifecycle / local desktop | `src/Scrobblix.Desktop/App.xaml.cs`, `MainWindow.xaml.cs`, `TrayIconService.cs`, Windows/desktop adapters | `scripts/test-desktop-ui-contracts.mjs`; Windows CI build | Single-instance activation, tray/Explorer restart, notifications, shutdown/relaunch and sleep/resume on real Windows. |
| FR-B — Last.fm authorization and actions | `LastFmDesktopAuthenticator.cs`, `LastFmRequestSigner.cs`, `LastFmScrobbleProvider.cs`, `ProviderActionProcessor.cs` | Domain/infrastructure assertions in `tests/Scrobblix.Domain.Tests`; Windows CI build | Live auth/token/session flow, remote accepted/ignored responses, revocation, retries and duplicate reconciliation with a dedicated test profile. |
| FR-C — Windows media observation | `src/Scrobblix.Windows/GsmTcPlaybackObservationSource.cs` and app polling/ingestion | Build/assertion coverage where included in current CI | Actual supported player list, sleep/resume, media properties changing late, source disappearing/reappearing and multi-day session tests. |
| FR-D — optional browser companion | `browser-extension/src/media-observer.ts`, `message-validation.ts`, `service-worker.ts`, `src/Scrobblix.NativeHost/Program.cs` | Browser build/type-check, `scripts/test-browser-security.mjs`, `scripts/validate-browser-manifests.mjs` | Chrome/Edge/Firefox installation, actual IDs/permissions, event delivery, host restarts, invalid identities and uninstall cleanup. Built-in YouTube origin mismatch is tracked in [issue #73](https://github.com/frpboy/Scrobblix/issues/73). |
| FR-E — canonical playback and eligibility | `src/Scrobblix.Domain/Eligibility/ScrobbleEligibility.cs`, `CanonicalPlayTracker`, `PlaybackTrackingService.cs`, source arbiter | Domain assertions in `tests/Scrobblix.Domain.Tests/Program.cs` | Pause/seek/replay/advertisement behavior against actual player timelines and Last.fm history over multiple days. |
| FR-F — durable provider queue | `src/Scrobblix.Application/Queue/ProviderActionProcessor.cs`, `src/Scrobblix.Infrastructure/Storage/SqliteProviderActionStore.cs` | Domain/infrastructure assertions in `tests/Scrobblix.Domain.Tests` | Forced process termination, expired lease recovery, outage/rate-limit, ambiguous POST and no-loss/no-duplicate long-session evidence. |
| FR-G — local history and data persistence | `src/Scrobblix.Infrastructure/Storage` migration/store implementations, `ILocalPlayStore`, `IPlaybackObservationStore` | Migration, store and state assertions in the domain/infrastructure test executable | Large-history performance, existing-database upgrades, backup/restore corruption and safe cleanup/deletion behavior on disposable data. |
| FR-H — user experience | `src/Scrobblix.Desktop/MainWindow.xaml`, `MainWindow.xaml.cs`, pages and WinUI resources | `scripts/test-desktop-ui-contracts.mjs`; build | Real GUI workflows, narrow sizes, theme/high contrast, keyboard-only, Narrator, 200% text, display scaling and focus/navigation review. |
| FR-I — diagnostics/support bundle | Diagnostics page/logger, `SupportBundleExporter.cs`, popup host diagnostic in `service-worker.ts` | Relevant assertions/source contracts in current Windows CI; inspect current workflow before claiming a particular canary coverage result | Seeded-canary archive inspection, forged field normalization, Save/cancel/error paths and accessibility on Windows. |
| NFR-P — performance / endurance | Local persistence, observation polling, browser ingress, queue processing, log/cache | Build/assertion tests only; no full soak in current CI | 8–24 hour soak with CPU/memory/DB/log growth, responsiveness, loss/duplicate reconciliation. |
| NFR-S — security | Credential Locker boundary, Native Messaging identity, local pipe, SQLite parameterization, archive validation | `scripts/test-browser-security.mjs`, manifest checks, relevant assertions | Formal threat review, installed-browser trust testing, signed package/provenance and whole-tree dependency/security review. |
| NFR-PR — privacy | Local-only playback path, credential handling, diagnostic allow-list, notification preference | Source-level/security assertions where present | Manual diagnostic canary/privacy and notification lock-screen checks; confirm all stated data-retention/deletion controls actually exist. |
| NFR-R — release/recovery | Preview release workflow and checksummed development ZIPs | `scripts/test-release-workflow.mjs`, Windows CI and preview packaging | Signed MSIX/App Installer or Store path, publisher verification, SBOM/provenance, clean-install/upgrade/rollback/uninstall and license review. |

## Traceability rules

A requirement is considered ready only when:
1. An implementation reference points to the actual code path.
2. Automated tests exercise the required behavior or there is a documented reason it is manual-only.
3. Failure, cancellation, privacy and recovery behavior are specified.
4. User-facing docs and relevant interface/schema/security records are updated.
5. The exact candidate SHA has a passing matching CI run.
6. Every manual-only requirement has a recorded result and sanitized evidence.

## Gaps intentionally not marked complete

- No signed production installer/updater or store distribution is established.
- No actual installed-browser E2E qualification is established.
- No live Last.fm authorization/scrobble acceptance or multi-day reconciliation result is established by this document update.
- Retention/deletion/JSON export and advanced statistics features require implementation-level checks before they can be advertised.
- A complete vulnerability, license, SBOM and external penetration review is outside the documentation validator.
