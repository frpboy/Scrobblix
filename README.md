# Scrobblix

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Public development previews; platform-specific qualification limits below\
> **Last updated:** 2026-11-10


Scrobblix is a local-first listening-history and Last.fm scrobbling project for **Windows and Android**. The Windows app uses WinUI 3 with an optional browser companion; the Android app uses Kotlin and Jetpack Compose with notification-listener/media-session collection. Both implementations maintain local history and track submission outcomes. They are development previews, not stable production releases.

This repository is the product and engineering source of truth for the personal open-source project maintained by **Rahul Muraleedharan** under the GitHub account **[`frpboy`](https://github.com/frpboy)**.

## Product promise

Scrobblix must answer three questions immediately:

1. **What am I listening to?**
2. **What have I listened to?**
3. **Why did or did not this track scrobble?**

## Downloads and preview releases

Public releases checked on 10 November 2026:

| Platform | Current preview | Download and limits |
|---|---|---|
| Android | [0.1.0-alpha.2](https://github.com/frpboy/Scrobblix/releases/tag/android-v0.1.0-alpha.2) | [Signed APK](https://github.com/frpboy/Scrobblix/releases/download/android-v0.1.0-alpha.2/Scrobblix-Android-v0.1.0-alpha.2.apk) and [SHA-256 checksum](https://github.com/frpboy/Scrobblix/releases/download/android-v0.1.0-alpha.2/SHA256SUMS.txt). Early development alpha for Android 8.0/API 26 or newer; not a Play release. |
| Windows | [0.1.0-beta.6](https://github.com/frpboy/Scrobblix/releases/tag/v0.1.0-beta.6) | [Windows x64 ZIP](https://github.com/frpboy/Scrobblix/releases/download/v0.1.0-beta.6/Scrobblix-Windows-x64-v0.1.0-beta.6.zip), Windows Test Bundle and optional Browser Companion. Use the x64 ZIP's asset digest on the release page; `SHA256SUMS.txt` covers the other ZIPs. Unsigned preview bundles, not a signed installer or stable production release. |

Read the linked release notes before installing. These platform-specific version numbers are separate release tracks, not a claim that one platform is more complete than the other. The [Releases page](https://github.com/frpboy/Scrobblix/releases) lists published versions and their exact assets.

Windows may require the matching Microsoft Windows App Runtime. Read the [release process](docs/RELEASE_PROCESS.md) for the preview workflow and the remaining signing, installer and real-Windows qualification gates. This README update changes documentation only; it does not rebuild, replace or republish release assets.

## Android and current qualification

The Android implementation is under `android/`. It includes notification-listener/media-session collection, local history, an account-fenced durable queue and Last.fm submission logic. It is not a Windows Browser Companion replacement. See [Android implementation status](docs/android/IMPLEMENTATION_STATUS.md) for implementation details; historical snapshots there must be read with the current release notes.

Android alpha.2 is built from merged source `5c54607c57b07d2e5e7f3dc6db46d09f36c71291`, after PRs [#161](https://github.com/frpboy/Scrobblix/pull/161) and [#162](https://github.com/frpboy/Scrobblix/pull/162). Its ordinary Android and Windows checks passed. The exact owner-signed alpha.1-to-alpha.2 APK pair also passed isolated, synthetic upgrade checks on API 26 and API 36, including preservation of synthetic history, preferences and account data. The same-commit app screenshots were inspected, including settings and privacy-disclosure screens.

That evidence is limited to the tested synthetic environments. It is **not** proof of real-provider authorization or scrobble delivery, a qualified update on a user's handset, Play readiness, or production reliability. Windows native About runtime qualification and real Windows listening, installer and signing gates remain open. Do not treat browser previews as installed-extension evidence.

Now Playing, a local eligible listen and a saved provider acknowledgement are different states. Unknown submissions are not automatically resent because Last.fm may already have received them. Do not reset app data, reinstall or replay uncertain submissions as a troubleshooting shortcut.

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
| License | Apache-2.0 for Scrobblix source code; third-party data remains governed by its own terms |

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
node scripts/test-browser-security.mjs
node scripts/validate-browser-manifests.mjs
node scripts/validate-docs.mjs
dotnet run --project src/Scrobblix.Desktop/Scrobblix.Desktop.csproj --configuration Release
```

### Existing clone

Make sure any local changes are committed or stashed first. The continuation
launcher targets `feat/private-beta-readiness`; it refuses to build while any
`Scrobblix.Desktop` process is open. Close the main window and any minimized-to-tray
instance before rebuilding. It will not kill processes automatically.

For automated validation without launching the app:

```powershell
dotnet restore Scrobblix.slnx
dotnet build Scrobblix.slnx --configuration Release
dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/test-browser-security.mjs
node scripts/validate-browser-manifests.mjs
node scripts/validate-docs.mjs
```

If WinUI reports `0x80040154 Class not registered`, install/register the
matching Windows App Runtime for this build and try again. The local browser-native-host installer requires the actual ID shown by each browser; Chrome and Edge IDs may differ, so pass both using `-ExtensionId @("CHROME_ID", "EDGE_ID")`. Browser registration is optional and is not required for desktop GSMTC detection.

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
- Privacy-safe support-bundle export is implemented on main, including a versioned, normalized source-status snapshot plus canary-redaction assertions; real Windows Save/export/accessibility qualification remains pending, alongside the soak, backup-migration and formal release/security review.

See `docs/PRIVATE_BETA_READINESS.md`, `Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md`, `Scrobblix-Docs/12_Testing/RELEASE_CHECKLIST.md` and `docs/IMPLEMENTATION_RESUME.md` before treating a build as release-ready.

## Anti-Slop UI work

The repository includes the [Anti-Slop core](skills/antislop/SKILL.md) and [UI skill](skills/antislop-ui/SKILL.md). Any visual audit/edit must follow the existing design direction in `Scrobblix-Docs/04_UserExperience/DESIGN_SYSTEM.md` and `docs/STITCH_UI_HANDOFF_2026-10-22.md`, resolve the documented DURING/AFTER mode, and pass the Anti-Slop Delivery Gate. Visual redesign has not started in the current crash/reliability change.

## Documentation map

- [`DOCUMENTATION_INDEX.md`](DOCUMENTATION_INDEX.md): repository-wide file-by-file catalogue, including the `docs/` entry point.
- [`DOCUMENTATION_VALIDATION.md`](DOCUMENTATION_VALIDATION.md): validation report, historical metrics, and explicit checks still to run.
- [`scripts/validate-docs.mjs`](scripts/validate-docs.mjs): automated Markdown structure and relative-link validation, also run by CI.
- [`00_Project/RECOMMENDATIONS.md`](Scrobblix-Docs/00_Project/RECOMMENDATIONS.md): consolidated product, architecture, privacy, release, and scope recommendations.
- [`00_Project`](Scrobblix-Docs/00_Project/README.md): vision, scope, principles, terminology, product rules, risks, and decisions.
- [`01_Product`](Scrobblix-Docs/01_Product/README.md): PRD, features, personas, journeys, metrics, and product strategy.
- [`02_Research`](Scrobblix-Docs/02_Research/README.md): competitor research, pain points, validation plan, and source notes.
- [`03_Requirements`](Scrobblix-Docs/03_Requirements/README.md): functional and non-functional requirements, use cases, user stories, and traceability.
- [`04_UserExperience`](Scrobblix-Docs/04_UserExperience/README.md): UI/UX vision, navigation, design system, screen specifications, accessibility, and content rules.
- [Stitch implementation handoff](Scrobblix-Docs/04_UserExperience/STITCH_IMPLEMENTATION_HANDOFF.md): reusable prompt, screen inventory, and design-to-XAML checklist for the external Stitch project.
- [Current Stitch UI handoff](docs/STITCH_UI_HANDOFF_2026-10-22.md): recorded lightweight Home screen, private Stitch project/artifact IDs, and boundaries between design reference and native WinUI implementation.
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

## Recommended delivery sequence — private beta readiness

1. Validate CI on the exact current main/candidate SHA and keep the manual matrix evidence-based.
2. Run multi-day Spotify sessions and reconcile qualifying local plays with a dedicated Last.fm test profile; cover pause/seek/repeats/ads, network/provider outages, sleep/wake and restart.
3. Qualify queue recovery, duplicate prevention, backup/restore and an 8–24 hour soak.
4. Test the optional Browser Companion in Chrome/Edge/Firefox and finish Native Messaging security review.
5. Qualify the privacy-safe support bundle, Home delivery states, source health, tray/notifications and Windows accessibility.
6. Package, sign and test clean install, upgrade, repair, rollback, update and uninstall on a clean Windows account/device.
7. Start private beta only after P0 evidence and security-critical gates are accepted; proceed to public beta/v1 after feedback and release review.

The principal feature surfaces already exist. This order prioritizes evidence and installability over additional features.

## Repository naming

Suggested public repositories:

- `frpboy/scrobblix` — main monorepo.
- `frpboy/scrobblix-docs` — optional separate docs repository if documentation becomes independently versioned.

The monorepo model is recommended initially so each code pull request can update the related specification and tests atomically.

## Maintainer and support

- Developer: [Rahul Muraleedharan](https://www.frpboy.in/)
- [GitHub](https://github.com/frpboy) and [LinkedIn](https://www.linkedin.com/in/rahul-muraleedharan-it)
- [Telegram support requests](https://t.me/FRPboy_Projects)

Maintained by [Rahul Muraleedharan (FrpBoy)](https://github.com/frpboy). Support helps maintain this personal open-source project. Scrobblix source is Apache-2.0-licensed; third-party components keep their own licenses.

[![Buy Me a Coffee](https://img.shields.io/badge/Buy_Me_a_Coffee-FrpBoy-FFDD00?logo=buymeacoffee&logoColor=black)](https://buymeacoffee.com/frpboy)
[![Become a Patron](https://img.shields.io/badge/Become_a_Patron-FrpBoy-FF424D?logo=patreon&logoColor=white)](https://www.patreon.com/c/FrpBoy)
