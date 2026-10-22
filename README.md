# Scrobblix

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Private-beta readiness; core surfaces implemented, real-world qualification pending  
> **Last updated:** 2026-10-22


Scrobblix is a privacy-first Windows listening companion and reliable Last.fm scrobbler. It detects playback from supported desktop applications and browser tabs, maintains a local listening history, explains why a track did or did not scrobble, and safely queues submissions when Last.fm or the network is unavailable.

This repository is the product and engineering source of truth for the personal open-source project maintained by **Rahul Muraleedharan** under the GitHub account **[`frpboy`](https://github.com/frpboy)**. It is not a Zabnix project.

## Product promise

Scrobblix must answer three questions immediately:

1. **What am I listening to?**
2. **What have I listened to?**
3. **Why did or did not this track scrobble?**

## Recommended implementation baseline

| Area | Decision |
|---|---|
| Desktop platform | Windows 11, with Windows 11 25H2+ as the supported release baseline |
| Desktop UI | C# / .NET 10 LTS / WinUI 3 / Windows App SDK |
| Playback discovery | Windows Global System Media Transport Controls (GSMTC) |
| Browser precision | TypeScript WebExtension with Chromium Manifest V3 and a Firefox variant |
| Native bridge | Browser Native Messaging; no unauthenticated localhost port |
| Local storage | SQLite, encrypted secrets in Windows Credential Locker |
| Architecture | Clean Architecture with explicit domain, application, infrastructure, and presentation boundaries |
| Distribution | Signed MSIX/App Installer first; Microsoft Store and WinGet later |
| License | MIT for Scrobblix source code; third-party data remains governed by its own terms |

The earlier Python/PySide6 concept remains viable for a prototype. It is not the selected production baseline because Scrobblix depends heavily on Windows-native media, notifications, packaging, credential storage, app lifecycle, and Fluent UI integration.

## Run locally on Windows

Scrobblix is an **unpackaged WinUI 3 desktop app**. For local development, use
Windows 11, the .NET 10 SDK, Node.js 22 or newer, Git for Windows, and the
Windows App Runtime version required by the project. Last.fm credentials are
optional for local playback/history; the app provides a Settings form to save
the API key and shared secret to Windows Credential Locker.

### Fresh clone

Open PowerShell:

```powershell
git clone https://github.com/frpboy/Scrobblix.git
Set-Location Scrobblix
git switch main
git pull --ff-only origin main
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
dotnet run --project src/Scrobblix.Desktop/Scrobblix.Desktop.csproj --configuration Release
```

### Existing clone

Make sure any local changes are committed or stashed first. Use the same mainline
commands above for a reproducible build. Close Scrobblix before rebuilding to avoid
MSBuild assembly-lock errors. The `update-and-run.ps1` helper belongs to the
continuation workflow and should only be used when intentionally working on that branch.

For automated validation without launching the app:

```powershell
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
```

If WinUI reports `0x80040154 Class not registered`, install/register the
matching Windows App Runtime for this build and try again. The current local
browser-native-host installer requires the built extension ID; browser
registration is optional and is not required for desktop GSMTC detection.

### Current implementation status

**Implemented in the current development baseline**

- SQLite-backed listening history, Today totals, search/filter history and CSV export.
- Canonical playback sessions that tolerate late metadata, GSMTC/browser source arbitration, and an idempotent durable queue with leases, bounded retry/backoff and recovery.
- Last.fm API signing, desktop authorization, secure Credential Locker storage, scrobbling, Now Playing, Love and Unlove action processing.
- MusicBrainz/Cover Art Archive/Last.fm metadata and artwork fallback with local cache and a bundled missing-art placeholder.
- Queue management, Statistics, Loved, Sources, Diagnostics, local numbered UTC logs and user-controlled Last.fm setup.
- Consistent SQLite backup, checksum/schema/integrity validation, staged restore and an automatic pre-restore safety snapshot. API keys, session keys and cached artwork are excluded from backup archives.
- Notification-area tray prototype: minimize hides the window while tracking continues; double-click restores the window and the context menu offers Open and Exit.
- Opt-in Windows app notifications. Notifications are off by default, provider acceptance is required before a success notice is shown, and artist/title details are hidden unless separately enabled.
- Chromium companion (Chrome/Edge) and a separate Firefox Manifest V3 development manifest, host allow-list and install/uninstall path.
- A clean-branch Windows CI pipeline for .NET, assertion tests, browser type-check/build, Chromium/Firefox manifest checks, desktop/native-host publishing and a downloadable test bundle.

The exact current-main commit and its matching workflow must be checked before treating a build as validated. See [main](https://github.com/frpboy/Scrobblix/tree/main) and [GitHub Actions](https://github.com/frpboy/Scrobblix/actions); a green run on an earlier SHA or an unmerged branch does not validate current main.

This is a **development build, not a signed production release**. Remaining release gates include:
- Multi-day Spotify manual test matrix and an 8–24 hour reliability soak, with duplicates/lost plays explicitly reconciled against a dedicated Last.fm test profile.
- Real on-device GUI checks for tray behavior, Explorer/taskbar restart, notification activation and privacy, and clean shutdown.
- Live Last.fm authorization/scrobble validation with the user's account, plus real Chrome/Edge and Firefox extension installation/bridge tests.
- Signed MSIX/App Installer and updater, install/upgrade/rollback/uninstall qualification, code-signing provenance and dependency/license/SBOM review.
- Further same-user IPC threat review; the current pipe peer check is an extra gate, not cryptographic proof of an executable's identity or a replacement for signed binaries.
- Support-bundle export with canary-secret redaction tests; accessibility, performance/long-run soak, backup migration across supported schema versions, and formal release/security review.

See `docs/PRIVATE_BETA_READINESS.md`, `Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md`, `Scrobblix-Docs/12_Testing/RELEASE_CHECKLIST.md` and `docs/IMPLEMENTATION_RESUME.md` before treating a build as release-ready.

## Documentation map

- [`DOCUMENTATION_INDEX.md`](DOCUMENTATION_INDEX.md): complete file-by-file catalogue.
- [`DOCUMENTATION_VALIDATION.md`](DOCUMENTATION_VALIDATION.md): generated structural validation results and limitations.
- [`00_Project/RECOMMENDATIONS.md`](Scrobblix-Docs/00_Project/RECOMMENDATIONS.md): consolidated product, architecture, privacy, release, and scope recommendations.
- [`00_Project`](Scrobblix-Docs/00_Project/README.md): vision, scope, principles, terminology, product rules, risks, and decisions.
- [`01_Product`](Scrobblix-Docs/01_Product/README.md): PRD, features, personas, journeys, metrics, and product strategy.
- [`02_Research`](Scrobblix-Docs/02_Research/README.md): competitor research, pain points, validation plan, and source notes.
- [`03_Requirements`](Scrobblix-Docs/03_Requirements/README.md): functional and non-functional requirements, use cases, user stories, and traceability.
- [`04_UserExperience`](Scrobblix-Docs/04_UserExperience/README.md): UI/UX vision, navigation, design system, screen specifications, accessibility, and content rules.
- [Stitch implementation handoff](Scrobblix-Docs/04_UserExperience/STITCH_IMPLEMENTATION_HANDOFF.md): reusable prompt, screen inventory, and design-to-XAML checklist for the external Stitch project.
- [Implementation resume guide](docs/IMPLEMENTATION_RESUME.md): current-main status, validation discipline and continue/build/run commands.
- [Private beta readiness](docs/PRIVATE_BETA_READINESS.md): ordered acceptance tracks and beta entry criteria.
- [Real-world manual test matrix](Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md): Spotify, Last.fm, recovery, soak, browser, Windows UX and beta decision record.
- [`05_Architecture`](Scrobblix-Docs/05_Architecture/README.md): system architecture, components, event flows, state machines, concurrency, and ADRs.
- [`06_Data`](Scrobblix-Docs/06_Data/README.md): database schema, retention, migrations, backup, and analytics data model.
- [`07_Interfaces`](Scrobblix-Docs/07_Interfaces/README.md): Last.fm integration, internal contracts, Native Messaging, schemas, and error model.
- [`08_DesktopApp`](Scrobblix-Docs/08_DesktopApp/README.md): Windows app behavior, GSMTC, tray, startup, notifications, packaging, and updates.
- [`09_BrowserExtension`](Scrobblix-Docs/09_BrowserExtension/README.md): extension architecture, adapters, permissions, browser differences, and privacy.
- [`10_CoreEngine`](Scrobblix-Docs/10_CoreEngine/README.md): playback tracking, metadata normalization, arbitration, eligibility, deduplication, queueing, and sync.
- [`11_SecurityPrivacy`](Scrobblix-Docs/11_SecurityPrivacy/README.md): threat model, credentials, privacy, telemetry, and legal considerations.
- [`12_Testing`](Scrobblix-Docs/12_Testing/README.md): unit, integration, E2E, performance, accessibility, and release qualification.
- [`13_Engineering`](Scrobblix-Docs/13_Engineering/README.md): repository layout, coding standards, Git workflow, CI/CD, versioning, and contribution workflow.
- [`14_Operations`](Scrobblix-Docs/14_Operations/README.md): logs, diagnostics, recovery, support bundles, and observability.
- [`15_Roadmap`](Scrobblix-Docs/15_Roadmap/README.md): milestones, prioritized backlog, and future platform strategy.
- [`16_AI_Agents`](Scrobblix-Docs/16_AI_Agents/README.md): mandatory rules and context for AI coding agents.
- [`17_UserDocumentation`](Scrobblix-Docs/17_UserDocumentation/README.md): installation, onboarding, daily use, troubleshooting, privacy, data, and removal guides.
- [`18_Brand`](Scrobblix-Docs/18_Brand/README.md): brand identity, product copy, release assets, and name/IP diligence.
- [`19_Localization`](Scrobblix-Docs/19_Localization/README.md): internationalization requirements and translation workflow.
- [`20_ProjectManagement`](Scrobblix-Docs/20_ProjectManagement/README.md): delivery phases, issue readiness, labels, release notes, and launch controls.
- [`diagrams`](Scrobblix-Docs/diagrams/README.md): Mermaid architecture, sequence, state, ER, and UI-flow diagrams.
- [`references`](Scrobblix-Docs/references/README.md): official source register and terminology references.

## Non-negotiable behavior

- Never submit a scrobble before Last.fm eligibility criteria are met.
- Preserve scrobbles through temporary network/API failures.
- Never silently discard a qualifying play.
- Every ignored, failed, delayed, or duplicate play must have a human-readable reason.
- Browser support must work without an extension when GSMTC metadata is sufficient; the extension is an optional precision layer.
- Do not record system audio or perform acoustic fingerprinting in the initial product.
- Local history and telemetry are separately controlled; telemetry is off by default.
- User credentials, Last.fm session keys, and release secrets never belong in plaintext configuration or Git.
- Documentation, tests, and implementation must change together.

## Suggested build sequence

1. Domain model, SQLite schema, and deterministic playback state machine.
2. GSMTC source adapter and diagnostic event stream.
3. Last.fm authentication, now-playing, queue, and scrobble synchronization.
4. Tray-first WinUI shell with Home, History, Queue, and Diagnostics.
5. Browser extension and Native Messaging host.
6. Statistics, loved tracks, export, backup, updater, and release hardening.

## Repository naming

Suggested public repositories:

- `frpboy/scrobblix` — main monorepo.
- `frpboy/scrobblix-docs` — optional separate docs repository if documentation becomes independently versioned.

The monorepo model is recommended initially so each code pull request can update the related specification and tests atomically.
