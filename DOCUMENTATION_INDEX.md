# Documentation Index

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Reviewed:** 2026-10-28  
> **Index scope:** repository documentation catalogue. Counts are a tracked-tree snapshot for 2026-10-28; the validation command reports current Markdown metrics.

This file is the repository-wide catalogue. Section README files provide guided reading paths; this index provides a file-by-file inventory.

## Repository statistics

The tracked tree contains 411 files and 258 Markdown documents on the documentation-validation branch as of 2026-10-28. Run `node scripts/validate-docs.mjs` to calculate the current approximate Markdown word count and verify relative link paths, non-empty files, and fenced-code balance. These counts are a snapshot, not a substitute for running the command.

## Repository root

| File | Purpose |
|---|---|
| [`.editorconfig`](.editorconfig) | **.editorconfig.** Repository configuration or non-Markdown project file. |
| [`.gitignore`](.gitignore) | **.gitignore.** Repository configuration or non-Markdown project file. |
| [`.markdownlint.json`](.markdownlint.json) | **.markdownlint.json.** Repository configuration or non-Markdown project file. |
| [`AGENTS.md`](AGENTS.md) | **AGENTS.md.** All coding agents working on Scrobblix must read: |
| [`CHANGELOG.md`](CHANGELOG.md) | **Changelog.** All notable changes to Scrobblix will be documented here. The project follows Semantic Versioning and a Keep-a-Changelog-style structure. |
| [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) | **Code of Conduct.** Scrobblix uses the Contributor Covenant principles: be respectful, assume good intent while requiring technical rigor, reject harassment and discrimination, keep criticism focused on work, and protect... |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | **Contributing to Scrobblix.** Scrobblix accepts bug reports, documentation improvements, test cases, source adapters, browser-site adapters, accessibility fixes, translations, and implementation changes that follow the documented... |
| [`DOCUMENTATION_INDEX.md`](DOCUMENTATION_INDEX.md) | **Documentation Index.** Repository-wide file catalogue and navigation index. |
| [`DOCUMENTATION_STATUS.md`](DOCUMENTATION_STATUS.md) | **Documentation Status.** Separates implementation inventory, exact-commit CI evidence and real-world qualification; points to private-beta readiness and the manual test matrix. |
| [`DOCUMENTATION_VALIDATION.md`](DOCUMENTATION_VALIDATION.md) | **Documentation Validation Report.** Generated static checks, repository counts, and validation limitations. |
| [`GOVERNANCE.md`](GOVERNANCE.md) | **Governance.** Scrobblix is currently a maintainer-led personal open-source project. |
| [`LICENSE`](LICENSE) | **LICENSE.** Repository configuration or non-Markdown project file. |
| [`README.md`](README.md) | **Scrobblix.** Scrobblix is a privacy-first Windows listening companion and reliable Last.fm scrobbler. It detects playback from supported desktop applications and browser tabs, maintains a local listening history,... |
| [`PRODUCTION_RELEASE_PREREQUISITES.md`](PRODUCTION_RELEASE_PREREQUISITES.md) | **Production Release Prerequisites.** External credentials/identities, Native Messaging threat review, live qualification, signed distribution and private-beta release gates. |
| [`SECURITY.md`](SECURITY.md) | **Security Policy.** Only the latest stable release and the active pre-release branch receive security fixes. Older versions may be fixed when practical but are not guaranteed. |
| [`SUPPORT.md`](SUPPORT.md) | **Support.** Before reporting a missed scrobble, capture: |
| [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) | **Third-Party Notices.** This file is a release-time registry, not a substitute for generated license review. |

## .github and repository templates

| File | Purpose |
|---|---|
| [`dependabot.yml`](.github/dependabot.yml) | Dependency update automation configuration. |
| [`ci.yml`](.github/workflows/ci.yml) | Windows build, tests, bundle checks, and documentation validation. |
| [`release.yml`](.github/workflows/release.yml) | Validated GitHub preview-release workflow; not a signed production installer. |
| [`bug_report.yml`](Scrobblix-Docs/.github/ISSUE_TEMPLATE/bug_report.yml) | Issue-report template. |
| [`config.yml`](Scrobblix-Docs/.github/ISSUE_TEMPLATE/config.yml) | Issue-template chooser configuration. |
| [`feature_request.yml`](Scrobblix-Docs/.github/ISSUE_TEMPLATE/feature_request.yml) | Feature request template. |
| [`PULL_REQUEST_TEMPLATE.md`](Scrobblix-Docs/.github/PULL_REQUEST_TEMPLATE.md) | Pull request checklist template. |


## Desktop UI evolution

| File | Purpose |
|---|---|
| [`Assets/providers/README.md`](src/Scrobblix.Desktop/Assets/providers/README.md) | Provenance and review ledger for optional local source/provider logos. No third-party marks are shipped without recorded usage approval. |
| [`SourceIdentityRegistry.cs`](src/Scrobblix.Desktop/SourceIdentityRegistry.cs) | Exact observed application-ID/HTTPS-origin identity resolution, with optional local assets and neutral fallback. |
| [`test-desktop-ui-contracts.mjs`](scripts/test-desktop-ui-contracts.mjs) | Source-level desktop UI contract tests in CI; does not replace Windows runtime/accessibility validation. |

## docs — Continuation, Private Beta and Developer Guides

| File | Purpose |
|---|---|
| [README.md](docs/README.md) | Entry point for this folder's documentation. |
| [DOCUMENTATION_REFRESH_2026-10-28.md](docs/DOCUMENTATION_REFRESH_2026-10-28.md) | Scope of the recent documentation refresh and remaining validation tasks. |
| [IMPLEMENTATION_RESUME.md](docs/IMPLEMENTATION_RESUME.md) | Current-main build/validation workflow and separation of code inventory from real-world acceptance. |
| [PRIVATE_BETA_READINESS.md](docs/PRIVATE_BETA_READINESS.md) | Acceptance tracks and private-beta go/no-go gates. |
| [RELEASE_PROCESS.md](docs/RELEASE_PROCESS.md) | Preview release workflow and distribution limitations. |
| [RELEASE_GATE_STATUS_2026-10-22.md](docs/RELEASE_GATE_STATUS_2026-10-22.md) | Historical release-gate snapshot; verify all items against current evidence. |
| [STITCH_UI_HANDOFF_2026-10-22.md](docs/STITCH_UI_HANDOFF_2026-10-22.md) | Design-to-native-WinUI handoff and implementation boundaries. |
| [MASTER_COMPLETION_PLAN_2026-10-22.md](docs/MASTER_COMPLETION_PLAN_2026-10-22.md) | Ordered implementation and evidence plan. |
| [MASTER_IMPLEMENTATION_PLAN.md](docs/MASTER_IMPLEMENTATION_PLAN.md) | Long-range implementation plan. |
| [CONTINUATION_CHECKPOINT_2026-10-22.md](docs/CONTINUATION_CHECKPOINT_2026-10-22.md) | Historical project continuation checkpoint. |
| [LOCAL_DEVELOPMENT_TROUBLESHOOTING.md](docs/LOCAL_DEVELOPMENT_TROUBLESHOOTING.md) | Build, launch and local environment troubleshooting. |
| [LOCAL_DIAGNOSTIC_LOGS.md](docs/LOCAL_DIAGNOSTIC_LOGS.md) | Local diagnostics and log handling. |
| [LOCAL_LASTFM_SETUP.md](docs/LOCAL_LASTFM_SETUP.md) | Local Last.fm configuration instructions. |
| [OWNER_SETUP_GUIDE.md](docs/OWNER_SETUP_GUIDE.md) | Maintainer setup and development bundle guide. |
| [BROWSER_STORE_RELEASE_CHECKLIST.md](docs/BROWSER_STORE_RELEASE_CHECKLIST.md) | Browser store release checklist; approval is not implied. |
| [ANTISLOP_INSTALL.md](docs/ANTISLOP_INSTALL.md) | Installation notes for repository UI/design skills. |
| [LIGHTWEIGHT_UI_EVOLUTION_PLAN.md](docs/LIGHTWEIGHT_UI_EVOLUTION_PLAN.md) | UI evolution plan. |
| [TRAY_QUICK_CONTROLS.md](docs/TRAY_QUICK_CONTROLS.md) | Notification-area quick controls guide. |
| [Implementation plan specification](docs/superpowers/plans/2026-10-21-scrobblix-complete-implementation.md) | Dated implementation plan. |
| [Design specification](docs/superpowers/specs/2026-10-21-scrobblix-complete-design.md) | Dated design specification. |
## 00_Project

| File | Purpose |
|---|---|
| [`ARCHITECTURE_DECISIONS.md`](Scrobblix-Docs/00_Project/ARCHITECTURE_DECISIONS.md) | **Architecture Decisions Index.** All future cross-cutting decisions use the template in ../13_Engineering/ADR_TEMPLATE.md. |
| [`ASSUMPTIONS_CONSTRAINTS.md`](Scrobblix-Docs/00_Project/ASSUMPTIONS_CONSTRAINTS.md) | **Assumptions and Constraints.** Documentation for assumptions and constraints. |
| [`GLOSSARY.md`](Scrobblix-Docs/00_Project/GLOSSARY.md) | **Glossary.** Documentation for glossary. |
| [`OPEN_QUESTIONS.md`](Scrobblix-Docs/00_Project/OPEN_QUESTIONS.md) | **Open Questions.** The following do not block initial domain and architecture work, but must be resolved before the stated gate. |
| [`PRODUCT_PRINCIPLES.md`](Scrobblix-Docs/00_Project/PRODUCT_PRINCIPLES.md) | **Product Principles.** Visual polish matters, but a scrobbler that loses events is defective. Eligibility, persistence, retry, recovery, and observability receive priority over secondary analytics or themes. |
| [`PRODUCT_RULES.md`](Scrobblix-Docs/00_Project/PRODUCT_RULES.md) | **Product Rules.** These rules are normative. An implementation change that violates one requires an explicit product decision and ADR. |
| [`PROJECT_VISION.md`](Scrobblix-Docs/00_Project/PROJECT_VISION.md) | **Project Vision.** Make listening-history capture on Windows dependable, understandable, and useful regardless of which supported desktop player or browser website is producing the music. |
| [`README.md`](Scrobblix-Docs/00_Project/README.md) | **00 — Project Foundation.** This section defines why Scrobblix exists, what it is allowed to become, and the vocabulary used throughout the repository. |
| [`RECOMMENDATIONS.md`](Scrobblix-Docs/00_Project/RECOMMENDATIONS.md) | **Consolidated Recommendations.** This document collects the principal product and engineering recommendations that are otherwise distributed across the repository. Each recommendation should be converted into an Architecture Decision... |
| [`RISKS.md`](Scrobblix-Docs/00_Project/RISKS.md) | **Risk Register.** Documentation for risk register. |
| [`SCOPE.md`](Scrobblix-Docs/00_Project/SCOPE.md) | **Scope.** A feature enters scope only when its user value, privacy implications, failure behavior, storage impact, testing plan, and maintenance ownership are documented. |
| [`TERMINOLOGY_AND_NAMING.md`](Scrobblix-Docs/00_Project/TERMINOLOGY_AND_NAMING.md) | **Terminology and Naming.** Use user language, not implementation language: |

## 01_Product

| File | Purpose |
|---|---|
| [`EXECUTIVE_SUMMARY.md`](Scrobblix-Docs/01_Product/EXECUTIVE_SUMMARY.md) | **Executive Summary.** Scrobblix is a native Windows listening companion that reliably scrobbles qualifying music to Last.fm across desktop applications and browser websites. It addresses the central trust problem of existing... |
| [`FEATURE_CATALOG.md`](Scrobblix-Docs/01_Product/FEATURE_CATALOG.md) | **Feature Catalog.** A feature is incomplete until its empty, loading, degraded, permission-denied, offline, error, and recovery states are specified and tested. |
| [`PERSONAS.md`](Scrobblix-Docs/01_Product/PERSONAS.md) | **Personas.** Profile: Uses YouTube Music, Spotify Web, SoundCloud, Bandcamp, or niche streaming sites. Rarely opens a desktop music player. |
| [`PRD.md`](Scrobblix-Docs/01_Product/PRD.md) | **Product Requirements Document.** Scrobblix detects music playback, tracks qualifying listening time, submits eligible plays to Last.fm, and preserves a local auditable listening record. It runs primarily in the Windows notification area... |
| [`PRODUCT_STRATEGY.md`](Scrobblix-Docs/01_Product/PRODUCT_STRATEGY.md) | **Product Strategy.** Scrobblix is positioned as the trustworthy Windows listening companion for users who care about Last.fm accuracy and local ownership. It should not compete with music players or recommendation platforms. |
| [`README.md`](Scrobblix-Docs/01_Product/README.md) | **01 — Product.** This section defines Scrobblix as a product rather than only an implementation. |
| [`RELEASE_CRITERIA.md`](Scrobblix-Docs/01_Product/RELEASE_CRITERIA.md) | **Product Release Criteria.** Documentation for product release criteria. |
| [`SUCCESS_METRICS.md`](Scrobblix-Docs/01_Product/SUCCESS_METRICS.md) | **Success Metrics.** Metrics are measured locally during development/test unless users explicitly opt into telemetry. Public product claims must be based on a documented test corpus, not inferred from opt-in production data alone. |
| [`USER_JOURNEYS.md`](Scrobblix-Docs/01_Product/USER_JOURNEYS.md) | **User Journeys.** 10. At eligibility, a scrobble queue item is persisted and submitted. 11. UI changes to “Scrobbled” only after acceptance. |

## 02_Research

| File | Purpose |
|---|---|
| [`COMPATIBILITY_RESEARCH.md`](Scrobblix-Docs/02_Research/COMPATIBILITY_RESEARCH.md) | **Compatibility Research Plan.** Each test stores synthetic/sanitized observations, expected canonical play, expected eligibility timeline, and expected terminal reason. Compatibility claims require reproducible fixtures and a documented... |
| [`COMPETITOR_ANALYSIS.md`](Scrobblix-Docs/02_Research/COMPETITOR_ANALYSIS.md) | **Competitor Analysis.** This is a product-planning comparison, not a definitive claim about every current version. Revalidate before publishing marketing statements. |
| [`PROBLEM_RESEARCH.md`](Scrobblix-Docs/02_Research/PROBLEM_RESEARCH.md) | **Problem Research.** 10. Provider dependence: no local history exists when provider submission fails. |
| [`README.md`](Scrobblix-Docs/02_Research/README.md) | **02 — Research.** Research documents collect evidence and hypotheses. They inform requirements but are not normative unless referenced by an approved decision. |
| [`SOURCE_NOTES.md`](Scrobblix-Docs/02_Research/SOURCE_NOTES.md) | **Source Notes.** The initial concept established: |
| [`USER_RESEARCH_PLAN.md`](Scrobblix-Docs/02_Research/USER_RESEARCH_PLAN.md) | **User Research Plan.** Recruit 12–20 users across casual Last.fm users, browser-first listeners, multi-player power users, privacy-conscious users, and technical contributors. Include current Windows 11 versions and major... |

## 03_Requirements

| File | Purpose |
|---|---|
| [`ACCEPTANCE_CRITERIA.md`](Scrobblix-Docs/03_Requirements/ACCEPTANCE_CRITERIA.md) | **Acceptance Criteria Catalog.** Given a 300-second track with valid artist/title, when confirmed playing time reaches 150 seconds, then the canonical play becomes eligible once, a queue item is persisted, and subsequent observations... |
| [`ERROR_AND_EDGE_CASES.md`](Scrobblix-Docs/03_Requirements/ERROR_AND_EDGE_CASES.md) | **Error and Edge-Case Catalog.** Every item must map to a reason code, recovery policy, and at least one test or documented unsupported behavior. |
| [`FUNCTIONAL_REQUIREMENTS.md`](Scrobblix-Docs/03_Requirements/FUNCTIONAL_REQUIREMENTS.md) | **Functional Requirements.** Priority: Must, Should, Could, Later. |
| [`NON_FUNCTIONAL_REQUIREMENTS.md`](Scrobblix-Docs/03_Requirements/NON_FUNCTIONAL_REQUIREMENTS.md) | **Non-Functional Requirements.** Documentation for non-functional requirements. |
| [`README.md`](Scrobblix-Docs/03_Requirements/README.md) | **03 — Requirements.** Documentation for 03 — requirements. |
| [`REQUIREMENTS_TRACEABILITY.md`](Scrobblix-Docs/03_Requirements/REQUIREMENTS_TRACEABILITY.md) | **Requirements Traceability.** This matrix is seeded for implementation. Each pull request adds concrete code/test references rather than deleting requirement links. |
| [`USE_CASES.md`](Scrobblix-Docs/03_Requirements/USE_CASES.md) | **Use Cases.** Actor: User Precondition: Network available; API account configured. Main flow: User initiates connection, browser authorization opens, app obtains authorized session, stores session key securely, fetches... |
| [`USER_STORIES.md`](Scrobblix-Docs/03_Requirements/USER_STORIES.md) | **User Stories.** Documentation for user stories. |

## 04_UserExperience

| File | Purpose |
|---|---|
| [`ACCESSIBILITY.md`](Scrobblix-Docs/04_UserExperience/ACCESSIBILITY.md) | **Accessibility Specification.** Automated accessibility scans are necessary but insufficient. Required manual passes include keyboard-only, Narrator, high contrast, 200% text scale, 150–200% display scale, and reduced motion. |
| [`CONTENT_DESIGN.md`](Scrobblix-Docs/04_UserExperience/CONTENT_DESIGN.md) | **Content Design.** Direct, factual, calm, and specific. Avoid blame and vague reassurance. |
| [`DESIGN_SYSTEM.md`](Scrobblix-Docs/04_UserExperience/DESIGN_SYSTEM.md) | **Design System.** Use WinUI 3 controls and Fluent design tokens wherever possible. Custom controls require a documented accessibility and theme strategy. |
| [`DIAGNOSTICS.md`](Scrobblix-Docs/04_UserExperience/DIAGNOSTICS.md) | **Diagnostics.** Make Scrobblix's internal pipeline understandable and repairable. |
| [`HISTORY_TIMELINE.md`](Scrobblix-Docs/04_UserExperience/HISTORY_TIMELINE.md) | **History and Timeline.** Make local listening activity auditable, searchable, and portable. |
| [`HOME.md`](Scrobblix-Docs/04_UserExperience/HOME.md) | **Home Screen.** Provide immediate confidence about current playback and system health. |
| [`INFORMATION_ARCHITECTURE.md`](Scrobblix-Docs/04_UserExperience/INFORMATION_ARCHITECTURE.md) | **Information Architecture.** Search results are grouped as: |
| [`INTERACTION_STATES.md`](Scrobblix-Docs/04_UserExperience/INTERACTION_STATES.md) | **Interaction and State Patterns.** Use skeletons for history/statistics; use explicit status text for health checks. Never show an indefinite spinner without an operation name and timeout/retry path. |
| [`NOTIFICATIONS.md`](Scrobblix-Docs/04_UserExperience/NOTIFICATIONS.md) | **Notifications.** Last.fm sign-in required 23 queued scrobbles are safe on this device. Reconnect to continue. |
| [`ONBOARDING.md`](Scrobblix-Docs/04_UserExperience/ONBOARDING.md) | **Onboarding.** Explain reliable scrobbling, local history, diagnostics, and no audio recording. |
| [`QUEUE.md`](Scrobblix-Docs/04_UserExperience/QUEUE.md) | **Queue.** Expose every remote action that has not reached a terminal successful/ignored state and provide safe recovery controls. |
| [`README.md`](Scrobblix-Docs/04_UserExperience/README.md) | **04 — User Experience.** This section defines the full desktop and companion experience. |
| [`SCREEN_SPECIFICATIONS.md`](Scrobblix-Docs/04_UserExperience/SCREEN_SPECIFICATIONS.md) | **Screen Specifications.** Every screen must define purpose, entry points, data dependencies, states, actions, accessibility, and diagnostics. |
| [`SETTINGS.md`](Scrobblix-Docs/04_UserExperience/SETTINGS.md) | **Settings.** Settings search indexes labels, descriptions, aliases, and actions. Managed policies are out of scope for v1 but the settings model should distinguish user values from future policy-enforced values. |
| [`SOURCES_AND_BROWSERS.md`](Scrobblix-Docs/04_UserExperience/SOURCES_AND_BROWSERS.md) | **Sources and Browsers.** Show exactly where playback can be detected and let users control inclusion, priority, and permissions. |
| [`STATISTICS.md`](Scrobblix-Docs/04_UserExperience/STATISTICS.md) | **Statistics.** Turn local history into useful insight without obscuring how each number is calculated. |
| [`TRAY_AND_MINI_PLAYER.md`](Scrobblix-Docs/04_UserExperience/TRAY_AND_MINI_PLAYER.md) | **Tray and Mini Player.** Do not change icons too rapidly; the tooltip carries exact state. |
| [`UX_VISION.md`](Scrobblix-Docs/04_UserExperience/UX_VISION.md) | **UX Vision.** Scrobblix should feel like a native Windows 11 companion that is calm during normal operation and exceptionally clear when something goes wrong. It is neither a music player nor a dense developer console. |
| [`WINDOWING_RESPONSIVE.md`](Scrobblix-Docs/04_UserExperience/WINDOWING_RESPONSIVE.md) | **Windowing and Responsive Behavior.** Set a practical minimum that preserves primary actions and avoids horizontal scrolling on core pages. Tables can use adaptive columns; technical detail may scroll horizontally only when unavoidable. |

## 05_Architecture

| File | Purpose |
|---|---|
| [`ARCHITECTURE.md`](Scrobblix-Docs/05_Architecture/ARCHITECTURE.md) | **System Architecture.** Scrobblix uses a modular monolith with Clean Architecture boundaries and event-driven background services. A monorepo contains the Windows UI, native host, browser extension, tests, documentation,... |
| [`COMPONENTS.md`](Scrobblix-Docs/05_Architecture/COMPONENTS.md) | **Component Catalog.** Each component has one primary responsibility. Cross-component workflows are coordinated by application use cases, not by circular callbacks between infrastructure services. |
| [`CONCURRENCY.md`](Scrobblix-Docs/05_Architecture/CONCURRENCY.md) | **Concurrency and Threading.** Use .NET hosted background services and asynchronous channels/queues. Avoid a shared mutable “current track” object accessed by UI, GSMTC callbacks, extension transport, and sync workers. |
| [`ADR-0001-dotnet-winui.md`](Scrobblix-Docs/05_Architecture/decisions/ADR-0001-dotnet-winui.md) | **ADR-0001 — Use .NET 10 and WinUI 3.** Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery... |
| [`ADR-0002-clean-architecture.md`](Scrobblix-Docs/05_Architecture/decisions/ADR-0002-clean-architecture.md) | **ADR-0002 — Use Clean Architecture boundaries.** Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery... |
| [`ADR-0003-hybrid-detection.md`](Scrobblix-Docs/05_Architecture/decisions/ADR-0003-hybrid-detection.md) | **ADR-0003 — Use GSMTC plus optional Browser Companion.** Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery... |
| [`ADR-0004-native-messaging.md`](Scrobblix-Docs/05_Architecture/decisions/ADR-0004-native-messaging.md) | **ADR-0004 — Use browser Native Messaging.** Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery... |
| [`ADR-0005-sqlite.md`](Scrobblix-Docs/05_Architecture/decisions/ADR-0005-sqlite.md) | **ADR-0005 — Use SQLite for local persistence.** Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery... |
| [`ADR-0006-local-first.md`](Scrobblix-Docs/05_Architecture/decisions/ADR-0006-local-first.md) | **ADR-0006 — Keep core operation local and backend-free.** Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery... |
| [`ADR-0007-provider-abstraction.md`](Scrobblix-Docs/05_Architecture/decisions/ADR-0007-provider-abstraction.md) | **ADR-0007 — Isolate Last.fm behind a provider contract.** Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery... |
| [`ADR-0008-msix-distribution.md`](Scrobblix-Docs/05_Architecture/decisions/ADR-0008-msix-distribution.md) | **ADR-0008 — Prefer signed MSIX and App Installer.** Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery... |
| [`DEPENDENCY_BOUNDARIES.md`](Scrobblix-Docs/05_Architecture/DEPENDENCY_BOUNDARIES.md) | **Dependency Boundaries.** Domain references nothing outward. Application references domain and abstractions. Infrastructure implements abstractions. Presentation invokes application use cases and maps read models. |
| [`EVENT_FLOWS.md`](Scrobblix-Docs/05_Architecture/EVENT_FLOWS.md) | **Event Flows.** 10. Sync worker leases due item(s), submits, maps response, and commits per-item outcome. 11. UI/read models receive change notifications after commit. |
| [`FAULT_TOLERANCE.md`](Scrobblix-Docs/05_Architecture/FAULT_TOLERANCE.md) | **Fault Tolerance and Recovery.** A failing source adapter is quarantined/restarted without stopping queue sync or UI. A provider outage does not stop detection/history. Analytics failure does not block scrobbling. Artwork failure never... |
| [`README.md`](Scrobblix-Docs/05_Architecture/README.md) | **05 — Architecture.** Documentation for 05 — architecture. |
| [`STATE_MACHINES.md`](Scrobblix-Docs/05_Architecture/STATE_MACHINES.md) | **State Machines.** Eligibility is a milestone orthogonal to playback state in implementation; the diagram simplifies it. A play can remain playing after eligibility. |

## 06_Data

| File | Purpose |
|---|---|
| [`ANALYTICS_MODEL.md`](Scrobblix-Docs/06_Data/ANALYTICS_MODEL.md) | **Analytics Data Model.** Analytics describe local observed behavior and must state the selected basis: detected, eligible, or provider-accepted. Do not merge them silently. |
| [`BACKUP_RESTORE.md`](Scrobblix-Docs/06_Data/BACKUP_RESTORE.md) | **Backup and Restore.** Format: versioned ZIP container with JSON manifest and database snapshot. The extension uses a Scrobblix-specific extension such as .scrobblix-backup only if file association is useful. |
| [`DATA_ARCHITECTURE.md`](Scrobblix-Docs/06_Data/DATA_ARCHITECTURE.md) | **Data Architecture.** Use Windows known folders and package APIs rather than hard-coded paths. The UI exposes actual paths. Packaged/unpackaged layouts must be abstracted so backup, support, and uninstall behavior remain consistent. |
| [`DATABASE_SCHEMA.md`](Scrobblix-Docs/06_Data/DATABASE_SCHEMA.md) | **Database Schema.** SQLite schema uses snake_case, UTC timestamps, explicit foreign keys, and migration-managed indexes. UUID/ULID text keys are preferred for cross-process/event correlation. The exact storage representation... |
| [`EXPORT_FORMATS.md`](Scrobblix-Docs/06_Data/EXPORT_FORMATS.md) | **Export Formats.** UTF-8 with header. Stable columns include schema version, play ID, local start/end ISO timestamps, artist, track, album, album artist, duration seconds, confirmed played seconds, eligibility time, local... |
| [`MIGRATIONS.md`](Scrobblix-Docs/06_Data/MIGRATIONS.md) | **Database Migrations.** Migrations are ordered, immutable after release, and applied before normal services start. A released migration file is never edited; corrections use a new migration. |
| [`README.md`](Scrobblix-Docs/06_Data/README.md) | **06 — Data.** Documentation for 06 — data. |
| [`RETENTION_AND_DELETION.md`](Scrobblix-Docs/06_Data/RETENTION_AND_DELETION.md) | **Retention and Deletion.** Removes the local play and dependent local observations/transitions/analytics. If a provider action is still pending, ask whether to cancel it. An already accepted Last.fm scrobble remains remote. |

## 07_Interfaces

| File | Purpose |
|---|---|
| [`AUTHENTICATION.md`](Scrobblix-Docs/07_Interfaces/AUTHENTICATION.md) | **Last.fm Authentication.** Prefer a safe documented desktop completion mechanism. If a custom URI callback is used, validate scheme registration, state/nonce, expected token, single-use, and source. If callback support is... |
| [`DEEP_LINKS.md`](Scrobblix-Docs/07_Interfaces/DEEP_LINKS.md) | **Deep Links and Activation.** Use a registered protocol such as scrobblix:// only after security review. Initial routes may include: |
| [`ERROR_TAXONOMY.md`](Scrobblix-Docs/07_Interfaces/ERROR_TAXONOMY.md) | **Error and Reason Taxonomy.** metadata.missing_artist, playback.track_too_short, playback.insufficient_confirmed_time, duplicate.matched_existing_play, provider.auth.invalid_session, provider.ignored.timestamp_too_old,... |
| [`INTERNAL_CONTRACTS.md`](Scrobblix-Docs/07_Interfaces/INTERNAL_CONTRACTS.md) | **Internal Application Contracts.** Presentation uses query DTOs designed for screens, including formatted-status inputs but not localized strings. Localization occurs in presentation/content layer using stable reason/status codes. |
| [`LASTFM_API.md`](Scrobblix-Docs/07_Interfaces/LASTFM_API.md) | **Last.fm API Integration.** Use the official Last.fm Web Services API over HTTPS. Write services use POST with UTF-8 form-encoded parameters and a signed api_sig. Always set explicit timeouts and a descriptive user agent/client... |
| [`MESSAGE_SCHEMAS.md`](Scrobblix-Docs/07_Interfaces/MESSAGE_SCHEMAS.md) | **Message Schemas.** title, artist, album, albumArtist, trackNumber, mbid, artworkUrl, sourceUrl (minimized/optional), and isAdvertisement/mediaKind only when adapter can assert. |
| [`NATIVE_MESSAGING_PROTOCOL.md`](Scrobblix-Docs/07_Interfaces/NATIVE_MESSAGING_PROTOCOL.md) | **Native Messaging Protocol.** Browser Native Messaging uses browser-managed standard input/output framing: each message is UTF-8 JSON preceded by a 32-bit native-endian length per browser specification. The host never writes logs to... |
| [`README.md`](Scrobblix-Docs/07_Interfaces/README.md) | **07 — Interfaces and Contracts.** Documentation for 07 — interfaces and contracts. |

## 08_DesktopApp

| File | Purpose |
|---|---|
| [`APP_LIFECYCLE.md`](Scrobblix-Docs/08_DesktopApp/APP_LIFECYCLE.md) | **Application Lifecycle.** Startup health remains visible; do not hide migration/repair failure behind a missing tray icon. |
| [`CREDENTIAL_STORAGE.md`](Scrobblix-Docs/08_DesktopApp/CREDENTIAL_STORAGE.md) | **Credential Storage.** Use Windows Credential Locker (PasswordVault/supported Windows credential APIs) through an ICredentialStore abstraction. Store the Last.fm session key under a namespaced resource. The database stores only... |
| [`GSMTC_INTEGRATION.md`](Scrobblix-Docs/08_DesktopApp/GSMTC_INTEGRATION.md) | **GSMTC Integration.** Use GlobalSystemMediaTransportControlsSessionManager.RequestAsync(), session enumeration, current-session and sessions-changed events, and per-session media properties/playback/timeline events. |
| [`NOTIFICATIONS_IMPLEMENTATION.md`](Scrobblix-Docs/08_DesktopApp/NOTIFICATIONS_IMPLEMENTATION.md) | **Windows Notifications Implementation.** Use Windows App SDK app notifications for local actionable notifications. Register activation routing and package identity requirements according to the chosen deployment mode. |
| [`OS_SUPPORT.md`](Scrobblix-Docs/08_DesktopApp/OS_SUPPORT.md) | **Operating System Support.** At documentation date, Windows 10 general support ended on 2025-10-14. Scrobblix v1 should officially target supported Windows 11 releases at launch. Current recommendation: Windows 11 25H2 or later,... |
| [`PACKAGING_INSTALLER.md`](Scrobblix-Docs/08_DesktopApp/PACKAGING_INSTALLER.md) | **Packaging and Installer.** Signed MSIX is the preferred baseline, potentially distributed through .appinstaller, GitHub Releases, Microsoft Store, and later WinGet. Final acceptance depends on validating startup, tray, Native... |
| [`README.md`](Scrobblix-Docs/08_DesktopApp/README.md) | **08 — Windows Desktop Application.** Documentation for 08 — windows desktop application. |
| [`TRAY_INTEGRATION.md`](Scrobblix-Docs/08_DesktopApp/TRAY_INTEGRATION.md) | **Tray Integration.** Persistent notification-area icon, dynamic tooltip/status, context menu, activation, and clean removal on exit/update. Tray operation must work when main window has never been created in the current session... |
| [`UPDATES.md`](Scrobblix-Docs/08_DesktopApp/UPDATES.md) | **Update System.** Stable and opt-in beta. Development/nightly builds are not offered to ordinary users by default. |
| [`WINDOWS_APP.md`](Scrobblix-Docs/08_DesktopApp/WINDOWS_APP.md) | **Windows App Specification.** C# on .NET 10 LTS, WinUI 3, Windows App SDK, XAML, Microsoft.Extensions.Hosting/DependencyInjection/Logging. Pin a supported stable Windows App SDK version at implementation start and update through... |

## 09_BrowserExtension

| File | Purpose |
|---|---|
| [`ARCHITECTURE.md`](Scrobblix-Docs/09_BrowserExtension/ARCHITECTURE.md) | **Browser Extension Architecture.** Content adapter emits a local model → service worker validates/coalesces → Native Messaging client sends versioned observation → desktop host acknowledges sequence. |
| [`CHROMIUM.md`](Scrobblix-Docs/09_BrowserExtension/CHROMIUM.md) | **Chromium Implementation.** Manifest V3 with service worker, content scripts registered only for granted/supported hosts, native messaging permission, strict content security policy, and no remote code. |
| [`DETECTION_ADAPTERS.md`](Scrobblix-Docs/09_BrowserExtension/DETECTION_ADAPTERS.md) | **Detection Adapter Specification.** Each adapter declares ID, version, URL match patterns, support tier, required permission, media candidates, metadata mapping, advertisement/non-music rules, chosen-by-user confidence, and fixtures. |
| [`EXTENSION_PRIVACY.md`](Scrobblix-Docs/09_BrowserExtension/EXTENSION_PRIVACY.md) | **Browser Companion Privacy.** Only data needed to identify and track media on enabled supported sites: media metadata, playback state, position/duration, adapter/site/tab identity, and limited source URL where required for user... |
| [`FIREFOX.md`](Scrobblix-Docs/09_BrowserExtension/FIREFOX.md) | **Firefox Implementation.** Share TypeScript adapter/core code where APIs align. Maintain a Firefox manifest and compatibility layer rather than assuming Chromium behavior. |
| [`NATIVE_HOST.md`](Scrobblix-Docs/09_BrowserExtension/NATIVE_HOST.md) | **Native Host.** Scrobblix.NativeHost.exe is a minimal broker launched by the browser. It parses framed JSON, authenticates/validates origin and protocol, forwards to the per-user main process, returns... |
| [`OVERVIEW.md`](Scrobblix-Docs/09_BrowserExtension/OVERVIEW.md) | **Browser Companion Overview.** The Browser Companion is an optional precision layer for browser music. It does not scrobble directly, hold Last.fm credentials, or replace the desktop engine. It emits structured local playback observations. |
| [`PERMISSIONS.md`](Scrobblix-Docs/09_BrowserExtension/PERMISSIONS.md) | **Extension Permissions.** Request the minimum permission at the moment it is needed and explain the direct benefit. |
| [`README.md`](Scrobblix-Docs/09_BrowserExtension/README.md) | **09 — Browser Companion.** Documentation for 09 — browser companion. |
| [`STORE_RELEASE.md`](Scrobblix-Docs/09_BrowserExtension/STORE_RELEASE.md) | **Browser Store Release.** Use beta/unlisted testing where available, then staged release. Desktop app and extension compatibility matrix defines supported protocol ranges so one can update before the other without breakage. |

## 10_CoreEngine

| File | Purpose |
|---|---|
| [`ANALYTICS_ENGINE.md`](Scrobblix-Docs/10_CoreEngine/ANALYTICS_ENGINE.md) | **Analytics Engine.** Committed canonical play changes/deletions and projector version. Provider status updates affect accepted/reliability projections without altering confirmed listening time. |
| [`DUPLICATE_PREVENTION.md`](Scrobblix-Docs/10_CoreEngine/DUPLICATE_PREVENTION.md) | **Duplicate Prevention.** Database unique constraints enforce queue-level uniqueness. |
| [`LOCAL_HISTORY.md`](Scrobblix-Docs/10_CoreEngine/LOCAL_HISTORY.md) | **Local History Engine.** Store canonical plays and outcomes. Raw observations/state transitions follow shorter configurable retention. History is independent of provider connection. |
| [`LOVED_TRACKS.md`](Scrobblix-Docs/10_CoreEngine/LOVED_TRACKS.md) | **Loved Tracks.** A love/unlove request is a provider action with local intent state: unknown, pending love, loved confirmed, pending unlove, not loved confirmed, failed/auth blocked. |
| [`METADATA_NORMALIZATION.md`](Scrobblix-Docs/10_CoreEngine/METADATA_NORMALIZATION.md) | **Metadata Normalization.** Create comparable and provider-ready values without destroying raw evidence or guessing. |
| [`PLAYBACK_ENGINE.md`](Scrobblix-Docs/10_CoreEngine/PLAYBACK_ENGINE.md) | **Playback Engine.** Validated immutable observations from GSMTC and Browser Companion, system lifecycle events, settings/exclusions, and injected clocks. |
| [`QUEUE_ENGINE.md`](Scrobblix-Docs/10_CoreEngine/QUEUE_ENGINE.md) | **Queue Engine.** Scrobble, love, unlove, and optionally ephemeral now-playing audit. Each action has provider/account, payload, state, due time, attempts, lease, and idempotency key. |
| [`README.md`](Scrobblix-Docs/10_CoreEngine/README.md) | **10 — Core Engine.** Documentation for 10 — core engine. |
| [`REASON_CODES.md`](Scrobblix-Docs/10_CoreEngine/REASON_CODES.md) | **Reason Codes.** Each code maps to severity, terminal/retry behavior, localized title/body, evidence fields, and recommended action IDs. Codes are never repurposed with different meaning. |
| [`SCROBBLE_ELIGIBILITY.md`](Scrobblix-Docs/10_CoreEngine/SCROBBLE_ELIGIBILITY.md) | **Scrobble Eligibility.** A candidate is scrobble-eligible when: |
| [`SOURCE_ARBITRATION.md`](Scrobblix-Docs/10_CoreEngine/SOURCE_ARBITRATION.md) | **Source Arbitration.** Merge reports of the same real playback and choose the most trustworthy fields/state. |
| [`SYNC_ENGINE.md`](Scrobblix-Docs/10_CoreEngine/SYNC_ENGINE.md) | **Provider Synchronization Engine.** Network state, provider health/circuit, account auth, queue due times, app lifecycle, user pause setting, and manual retry. |

## 11_SecurityPrivacy

| File | Purpose |
|---|---|
| [`DATA_CLASSIFICATION.md`](Scrobblix-Docs/11_SecurityPrivacy/DATA_CLASSIFICATION.md) | **Data Classification.** Data moves only to the same or stronger protection class unless user knowingly exports/shares it. Derived analytics retain the sensitivity of source history. Hashing a track/URL does not automatically make... |
| [`IPC_SECURITY.md`](Scrobblix-Docs/11_SecurityPrivacy/IPC_SECURITY.md) | **IPC and Native Messaging Security.** Native host manifests permit only exact published extension IDs. Development IDs are separate and never accepted by production host unless a clearly labeled development build is used. |
| [`LEGAL_API_COMPLIANCE.md`](Scrobblix-Docs/11_SecurityPrivacy/LEGAL_API_COMPLIANCE.md) | **Legal and API Compliance Checklist.** Perform name/domain/store/trademark clearance before significant promotion. “Scrobblix” should include a disclaimer that it is not affiliated with or endorsed by Last.fm. |
| [`PRIVACY_NOTICE.md`](Scrobblix-Docs/11_SecurityPrivacy/PRIVACY_NOTICE.md) | **Privacy Notice Draft.** Scrobblix processes playback metadata on your Windows device to maintain local listening history and, when you connect Last.fm, submit eligible listening events to Last.fm. Scrobblix does not record system... |
| [`README.md`](Scrobblix-Docs/11_SecurityPrivacy/README.md) | **11 — Security, Privacy, and Compliance.** Documentation for 11 — security, privacy, and compliance. |
| [`SECRETS_MANAGEMENT.md`](Scrobblix-Docs/11_SecurityPrivacy/SECRETS_MANAGEMENT.md) | **Secrets Management.** No secret values in Git, examples, fixtures, issues, logs, screenshots, or generated documentation. Provide .env.example only with dummy names. Enable GitHub secret scanning and push protection where available. |
| [`SECURITY_ARCHITECTURE.md`](Scrobblix-Docs/11_SecurityPrivacy/SECURITY_ARCHITECTURE.md) | **Security Architecture.** Least privilege, secure defaults, explicit trust boundaries, defense in depth, local minimization, signed supply chain, fail closed for security decisions, fail usefully for availability. |
| [`TELEMETRY_CRASH_REPORTING.md`](Scrobblix-Docs/11_SecurityPrivacy/TELEMETRY_CRASH_REPORTING.md) | **Telemetry and Crash Reporting.** Telemetry is off. No listening history, page metadata, or crash dump is uploaded automatically. |
| [`THREAT_MODEL.md`](Scrobblix-Docs/11_SecurityPrivacy/THREAT_MODEL.md) | **Threat Model.** Assets include Last.fm session keys/API credentials, local listening history, queue integrity, extension permissions, updater trust, package signing identity, diagnostic data, and the user's ability to... |
| [`UPDATE_SUPPLY_CHAIN.md`](Scrobblix-Docs/11_SecurityPrivacy/UPDATE_SUPPLY_CHAIN.md) | **Update and Supply-Chain Security.** Use lock files, automated update PRs, vulnerability alerts, allowed-license review, and removal of abandoned/unnecessary packages. Avoid adding packages for trivial functionality. Native/browser... |

## 12_Testing

| File | Purpose |
|---|---|
| [`ACCESSIBILITY_TESTS.md`](Scrobblix-Docs/12_Testing/ACCESSIBILITY_TESTS.md) | **Accessibility Tests.** WinUI accessibility tooling, UI Automation tree checks, contrast/static analysis where supported, missing labels/automation IDs, keyboard focus traps. |
| [`BROWSER_TESTS.md`](Scrobblix-Docs/12_Testing/BROWSER_TESTS.md) | **Browser Extension Tests.** Adapter parsing with saved synthetic DOM/JSON fixtures, generic Media Session/media-element logic, permission decisions, coalescing, protocol schemas, reconnect/snapshot behavior. |
| [`COMPATIBILITY_MATRIX.md`](Scrobblix-Docs/12_Testing/COMPATIBILITY_MATRIX.md) | **Compatibility Matrix.** For each claimed player/site record version, detection paths, metadata fields, pause/seek/repeat behavior, multiple-session behavior, known limitations, and pass date. A marketing support claim must link to... |
| [`DEFINITION_OF_DONE.md`](Scrobblix-Docs/12_Testing/DEFINITION_OF_DONE.md) | **Definition of Done.** A change is done only when: |
| [`E2E_UI_TESTS.md`](Scrobblix-Docs/12_Testing/E2E_UI_TESTS.md) | **End-to-End and UI Tests.** Use Windows UI Automation/Appium-compatible tooling supported by WinUI at implementation time. Stable automation IDs are part of UI contract. Do not rely solely on screenshots or pixel coordinates. |
| [`INTEGRATION_TESTS.md`](Scrobblix-Docs/12_Testing/INTEGRATION_TESTS.md) | **Integration Tests.** Schema creation, every migration path, transaction/crash boundaries, foreign keys/unique constraints, queue leases, concurrent readers/writers, backup/restore, corruption handling, retention, export. |
| [`PERFORMANCE_TESTS.md`](Scrobblix-Docs/12_Testing/PERFORMANCE_TESTS.md) | **Performance Tests.** Measure idle and active CPU, private working set, wakeups/timers, disk writes, startup, UI latency, history scrolling/search, extension latency, long-run leak over 24–72 hours. |
| [`README.md`](Scrobblix-Docs/12_Testing/README.md) | **12 — Testing and Quality.** Documentation for 12 — testing and quality. |
| [`RELEASE_CHECKLIST.md`](Scrobblix-Docs/12_Testing/RELEASE_CHECKLIST.md) | **Release Checklist.** Documentation for release checklist. |
| [`REAL_WORLD_MANUAL_TEST_MATRIX.md`](Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md) | **Real-world Manual Test Matrix.** Spotify desktop, Last.fm profile reconciliation, duplicate/restart recovery, soak, backup/restore, browser, Windows UX and support-bundle qualification; all unexecuted cases stay NOT RUN. |
| [`SECURITY_TESTS.md`](Scrobblix-Docs/12_Testing/SECURITY_TESTS.md) | **Security Tests.** SAST/analyzers, dependency vulnerability/license scan, secret scan, SBOM, signed artifact verification, CI permission review. |
| [`TEST_FIXTURES.md`](Scrobblix-Docs/12_Testing/TEST_FIXTURES.md) | **Test Fixtures and Synthetic Data.** No real user history, credentials, private URLs, copyrighted artwork/audio, or production secrets. Fixtures are deterministic, minimal, and clearly synthetic. |
| [`TEST_STRATEGY.md`](Scrobblix-Docs/12_Testing/TEST_STRATEGY.md) | **Test Strategy.** Scrobblix prioritizes data durability, deterministic timing, duplicate prevention, source compatibility, security/privacy, and diagnostic explainability. Test pyramid: many domain unit/property tests,... |
| [`UNIT_TESTS.md`](Scrobblix-Docs/12_Testing/UNIT_TESTS.md) | **Unit and Property Tests.** Examples: confirmed played time never decreases; paused time never increases it; eligibility fires at most once; one canonical play/provider creates at most one scrobble action; retry preserves original... |

## 13_Engineering

| File | Purpose |
|---|---|
| [`ADR_TEMPLATE.md`](Scrobblix-Docs/13_Engineering/ADR_TEMPLATE.md) | **Architecture Decision Record Template.** ADRs are concise, immutable after acceptance except status/link corrections. A changed decision uses a new ADR. |
| [`CI_CD.md`](Scrobblix-Docs/13_Engineering/CI_CD.md) | **CI/CD.** Windows integration/E2E/browser matrix, performance smoke/regression, package build, SBOM, unsigned test artifacts with retention. |
| [`CODING_STANDARDS.md`](Scrobblix-Docs/13_Engineering/CODING_STANDARDS.md) | **Coding Standards.** Strict mode, explicit protocol/domain types, schema validation at boundary, no any without documented exception, no remote/eval code, content-script DOM access isolated in adapters, promises handled,... |
| [`DEPENDENCY_MANAGEMENT.md`](Scrobblix-Docs/13_Engineering/DEPENDENCY_MANAGEMENT.md) | **Dependency Management.** Prefer platform/BCL and mature maintained packages with compatible license, active security process, minimal transitive graph, and clear Windows/.NET 10 support. Avoid dependencies for trivial helpers. |
| [`DEVELOPER_SETUP.md`](Scrobblix-Docs/13_Engineering/DEVELOPER_SETUP.md) | **Developer Setup.** Current supported Windows 11, Visual Studio/Build Tools supporting .NET 10 and WinUI 3, Windows App SDK workload, Git, Node.js active LTS and package manager locked by repository, Chrome/Edge/Firefox test... |
| [`GIT_WORKFLOW.md`](Scrobblix-Docs/13_Engineering/GIT_WORKFLOW.md) | **Git Workflow.** Protected main remains releasable. Short-lived feature/, fix/, docs/, chore/, release/ branches. No long-lived develop branch initially. |
| [`ISSUE_TRIAGE.md`](Scrobblix-Docs/13_Engineering/ISSUE_TRIAGE.md) | **Issue Triage.** Type (bug, feature, docs, security, compatibility), area, severity/priority, source/browser/player, status, needs-reproduction, good-first-issue. |
| [`PULL_REQUEST_CHECKLIST.md`](Scrobblix-Docs/13_Engineering/PULL_REQUEST_CHECKLIST.md) | **Pull Request Checklist.** Documentation for pull request checklist. |
| [`README.md`](Scrobblix-Docs/13_Engineering/README.md) | **13 — Engineering.** Documentation for 13 — engineering. |
| [`RELEASE_PROCESS.md`](Scrobblix-Docs/13_Engineering/RELEASE_PROCESS.md) | **Release Process.** 10. Monitor issues/API key/update health; promote rollout. 11. Archive evidence and update documentation status. |
| [`REPOSITORY_STRUCTURE.md`](Scrobblix-Docs/13_Engineering/REPOSITORY_STRUCTURE.md) | **Repository Structure.** No generic utils dumping ground. Shared code must have a defined owner/domain. Generated files are separated and reproducible. Tests mirror production boundaries. Documentation lives with code or under docs... |
| [`VERSIONING.md`](Scrobblix-Docs/13_Engineering/VERSIONING.md) | **Versioning.** Semantic Versioning: MAJOR.MINOR.PATCH, with -alpha.N, -beta.N, -rc.N. Version applies to desktop product; extension/native protocol have compatible independent versions recorded in About/diagnostics. |

## 14_Operations

| File | Purpose |
|---|---|
| [`HEALTH_DIAGNOSTICS.md`](Scrobblix-Docs/14_Operations/HEALTH_DIAGNOSTICS.md) | **Health and Diagnostics Model.** Each component implements named health checks returning status (healthy, degraded, unhealthy, unknown), checked time, stable reason, safe evidence, and repair actions. |
| [`INCIDENT_RESPONSE.md`](Scrobblix-Docs/14_Operations/INCIDENT_RESPONSE.md) | **Incident Response.** Security vulnerability, malicious/bad update, API key suspension/abuse, widespread data loss/duplicate scrobbles, browser-store removal, provider policy change, broken migration. |
| [`LOGGING.md`](Scrobblix-Docs/14_Operations/LOGGING.md) | **Logging.** Trace (temporary detailed events), Debug, Information, Warning, Error, Critical. Public default is Information with selected noisy categories reduced. |
| [`OBSERVABILITY.md`](Scrobblix-Docs/14_Operations/OBSERVABILITY.md) | **Local Observability.** Observation rate/drop/coalesce, active sessions/plays, eligibility latency, queue counts/oldest age/attempts, provider latency/classification, database latency/size, adapter completeness, extension... |
| [`README.md`](Scrobblix-Docs/14_Operations/README.md) | **14 — Operations and Supportability.** Documentation for 14 — operations and supportability. |
| [`RECOVERY_RUNBOOKS.md`](Scrobblix-Docs/14_Operations/RECOVERY_RUNBOOKS.md) | **Recovery Runbooks.** Block provider queue → notify once → reconnect → verify → unblock → batch retry. |
| [`SUPPORT_BUNDLE.md`](Scrobblix-Docs/14_Operations/SUPPORT_BUNDLE.md) | **Support Bundle.** Privacy-safe diagnostics-only ZIP allow-list, constrained operational-event fields, explicit exclusions, SHA-256 and remaining canary/Windows qualification gates. |

## 15_Roadmap

| File | Purpose |
|---|---|
| [`FUTURE_IDEAS.md`](Scrobblix-Docs/15_Roadmap/FUTURE_IDEAS.md) | **Future Ideas.** Potential ideas, not commitments: |
| [`MILESTONES.md`](Scrobblix-Docs/15_Roadmap/MILESTONES.md) | **Milestones.** Reconciles implemented surfaces with pending evidence gates; current milestone is private-beta readiness followed by signed installable release qualification. |
| [`PRIORITIZED_BACKLOG.md`](Scrobblix-Docs/15_Roadmap/PRIORITIZED_BACKLOG.md) | **Prioritized Backlog.** Verification-first backlog covering real playback/Last.fm reconciliation, duplicate and queue recovery, backup/restore, browser security, support bundle and signed install qualification. |
| [`README.md`](Scrobblix-Docs/15_Roadmap/README.md) | **15 — Roadmap.** Documentation for 15 — roadmap. |
| [`V1_RELEASE_PLAN.md`](Scrobblix-Docs/15_Roadmap/V1_RELEASE_PLAN.md) | **v1 Release Plan.** Private-beta-first sequence with exact-SHA CI, manual listening evidence, soak/recovery, security, accessibility and signed installer entry criteria. |

## 16_AI_Agents

| File | Purpose |
|---|---|
| [`AI_AGENT_GUIDE.md`](Scrobblix-Docs/16_AI_Agents/AI_AGENT_GUIDE.md) | **AI Agent Guide.** Do not silently override docs with assumptions. Flag conflicts and update the authoritative document in the same change when a decision changes. |
| [`DOCUMENTATION_UPDATE_POLICY.md`](Scrobblix-Docs/16_AI_Agents/DOCUMENTATION_UPDATE_POLICY.md) | **Documentation Update Policy.** An AI agent must not create duplicate contradictory docs. Update authoritative files and links. New documents require index entries. |
| [`IMPLEMENTATION_PROMPT.md`](Scrobblix-Docs/16_AI_Agents/IMPLEMENTATION_PROMPT.md) | **Implementation Prompt Template.** Documentation for implementation prompt template. |
| [`PROJECT_CONTEXT.md`](Scrobblix-Docs/16_AI_Agents/PROJECT_CONTEXT.md) | **AI Project Context.** Scrobblix is Rahul Muraleedharan's personal open-source project (frpboy), not a Zabnix project. |
| [`README.md`](Scrobblix-Docs/16_AI_Agents/README.md) | **16 — AI Agent Guidance.** Documentation for 16 — ai agent guidance. |
| [`REVIEW_PROMPT.md`](Scrobblix-Docs/16_AI_Agents/REVIEW_PROMPT.md) | **Code Review Prompt Template.** Documentation for code review prompt template. |

## 17_UserDocumentation

| File | Purpose |
|---|---|
| [`BROWSER_COMPANION.md`](Scrobblix-Docs/17_UserDocumentation/BROWSER_COMPANION.md) | **Browser Companion Guide.** The Browser Companion improves detection when a browser or site does not expose complete, timely, or unambiguous Windows media-session metadata. It can identify the exact browser tab/site, inspect supported... |
| [`CONNECT_LASTFM.md`](Scrobblix-Docs/17_UserDocumentation/CONNECT_LASTFM.md) | **Connect Last.fm.** Scrobblix never asks the user to type their Last.fm password into the application. |
| [`DAILY_USE.md`](Scrobblix-Docs/17_UserDocumentation/DAILY_USE.md) | **Daily Use.** Scrobblix normally runs in the Windows notification area. Closing the main window should hide it rather than terminate the background engine when tray operation is enabled. Exit Scrobblix must be a distinct... |
| [`DATA_MANAGEMENT.md`](Scrobblix-Docs/17_UserDocumentation/DATA_MANAGEMENT.md) | **Data Management.** Users can export selected history as CSV or versioned JSON. Export includes the chosen date range and clearly labeled local/provider statuses. Secrets and internal authentication material are never exported. |
| [`FAQ.md`](Scrobblix-Docs/17_UserDocumentation/FAQ.md) | **Frequently Asked Questions.** No. The initial product uses media metadata and playback state from Windows and, optionally, a browser extension. Audio capture and acoustic fingerprinting are explicitly outside the v1 scope. |
| [`GETTING_STARTED.md`](Scrobblix-Docs/17_UserDocumentation/GETTING_STARTED.md) | **Getting Started.** Scrobblix is designed for supported Windows 11 systems. A Last.fm account is required only when the user wants remote scrobbling; local listening history can operate without one. Browser detection works... |
| [`INSTALLATION.md`](Scrobblix-Docs/17_UserDocumentation/INSTALLATION.md) | **Installation Guide.** This is the recommended release channel. The package should publish verified identity, version, publisher, requested capabilities, and update source. Users should not be instructed to bypass Windows... |
| [`PRIVACY_CONTROLS.md`](Scrobblix-Docs/17_UserDocumentation/PRIVACY_CONTROLS.md) | **Privacy Controls for Users.** Scrobblix exposes separate controls for: |
| [`README.md`](Scrobblix-Docs/17_UserDocumentation/README.md) | **User Documentation.** End-user installation, onboarding, daily operation, troubleshooting, privacy, data management, and removal guidance. |
| [`TROUBLESHOOTING.md`](Scrobblix-Docs/17_UserDocumentation/TROUBLESHOOTING.md) | **Troubleshooting Guide.** Do not immediately reinstall or clear data; diagnostics should identify the failing layer first. |
| [`UNINSTALLATION.md`](Scrobblix-Docs/17_UserDocumentation/UNINSTALLATION.md) | **Uninstallation.** The user should decide whether to: |

## 18_Brand

| File | Purpose |
|---|---|
| [`ASSET_CHECKLIST.md`](Scrobblix-Docs/18_Brand/ASSET_CHECKLIST.md) | **Brand and Release Asset Checklist.** Documentation for brand and release asset checklist. |
| [`BRAND_GUIDELINES.md`](Scrobblix-Docs/18_Brand/BRAND_GUIDELINES.md) | **Brand Guidelines.** Scrobblix represents reliable, transparent listening history. The brand should feel precise, calm, modern, and technically trustworthy rather than nostalgic, noisy, or “hacker themed.” |
| [`NAME_IP_REVIEW.md`](Scrobblix-Docs/18_Brand/NAME_IP_REVIEW.md) | **Product Name and Intellectual Property Review.** This is an engineering/legal diligence checklist, not legal advice. |
| [`PRODUCT_COPY.md`](Scrobblix-Docs/18_Brand/PRODUCT_COPY.md) | **Product Copy Guide.** Primary recommendation: Your listening, reliably recorded. |
| [`README.md`](Scrobblix-Docs/18_Brand/README.md) | **Brand Documentation.** Product identity, visual direction, UI copy, launch assets, and name/IP diligence. |
| [`VISUAL_IDENTITY.md`](Scrobblix-Docs/18_Brand/VISUAL_IDENTITY.md) | **Visual Identity Direction.** Use a distinctive abstract mark combining one or more of: |

## 19_Localization

| File | Purpose |
|---|---|
| [`INTERNATIONALIZATION.md`](Scrobblix-Docs/19_Localization/INTERNATIONALIZATION.md) | **Internationalization Requirements.** English is the initial authoring language, but implementation must be localization-ready. User-facing strings do not belong in code-behind, domain exceptions, protocol constants, database values, or image... |
| [`LOCALIZATION_WORKFLOW.md`](Scrobblix-Docs/19_Localization/LOCALIZATION_WORKFLOW.md) | **Localization Workflow.** Community translations require contributor attribution, review rules, and a maintainer who can judge quality. Machine translation may be used only as a draft and must be labeled/reviewed before a supported... |
| [`README.md`](Scrobblix-Docs/19_Localization/README.md) | **Internationalization and Localization.** Documentation for internationalization and localization. |

## 20_ProjectManagement

| File | Purpose |
|---|---|
| [`DEFINITION_OF_READY.md`](Scrobblix-Docs/20_ProjectManagement/DEFINITION_OF_READY.md) | **Definition of Ready.** An implementation issue is ready only when it has: |
| [`DELIVERY_PLAN.md`](Scrobblix-Docs/20_ProjectManagement/DELIVERY_PLAN.md) | **Delivery Plan.** Exit criterion: deterministic test suite proves that one logical play yields no more than one logical scrobble action across pause, seek, replay, restart, overlap, and retry cases. |
| [`ISSUE_LABELS.md`](Scrobblix-Docs/20_ProjectManagement/ISSUE_LABELS.md) | **Issue Label Taxonomy.** Labels do not replace milestones, assignees, linked pull requests, or written acceptance criteria. |
| [`MAINTAINER_CHECKLIST.md`](Scrobblix-Docs/20_ProjectManagement/MAINTAINER_CHECKLIST.md) | **Maintainer Checklist.** Use ../12_Testing/RELEASE_CHECKLIST.md plus: |
| [`OPEN_SOURCE_LAUNCH_CHECKLIST.md`](Scrobblix-Docs/20_ProjectManagement/OPEN_SOURCE_LAUNCH_CHECKLIST.md) | **Open-Source Launch Checklist.** Documentation for open-source launch checklist. |
| [`README.md`](Scrobblix-Docs/20_ProjectManagement/README.md) | **Project Management.** Delivery sequencing, issue readiness, labels, maintainer controls, release communication, and open-source launch preparation. |
| [`RELEASE_NOTES_TEMPLATE.md`](Scrobblix-Docs/20_ProjectManagement/RELEASE_NOTES_TEMPLATE.md) | **Scrobblix Release X.Y.Z.** Describe the user-visible value in two to five concise points. |

## diagrams

| File | Purpose |
|---|---|
| [`AUTH_SEQUENCE.md`](Scrobblix-Docs/diagrams/AUTH_SEQUENCE.md) | **Last.fm Authentication Sequence.** Documentation for last.fm authentication sequence. |
| [`BROWSER_FLOW.md`](Scrobblix-Docs/diagrams/BROWSER_FLOW.md) | **Browser Companion Flow.** Documentation for browser companion flow. |
| [`DATABASE_ERD.md`](Scrobblix-Docs/diagrams/DATABASE_ERD.md) | **Database ERD.** Documentation for database erd. |
| [`DEPLOYMENT.md`](Scrobblix-Docs/diagrams/DEPLOYMENT.md) | **Deployment Diagram.** Documentation for deployment diagram. |
| [`PLAY_STATE.md`](Scrobblix-Docs/diagrams/PLAY_STATE.md) | **Play State Diagram.** Documentation for play state diagram. |
| [`PLAYBACK_SEQUENCE.md`](Scrobblix-Docs/diagrams/PLAYBACK_SEQUENCE.md) | **Playback Sequence.** Documentation for playback sequence. |
| [`QUEUE_RETRY_SEQUENCE.md`](Scrobblix-Docs/diagrams/QUEUE_RETRY_SEQUENCE.md) | **Queue Retry Sequence.** Documentation for queue retry sequence. |
| [`QUEUE_STATE.md`](Scrobblix-Docs/diagrams/QUEUE_STATE.md) | **Queue State Diagram.** Documentation for queue state diagram. |
| [`README.md`](Scrobblix-Docs/diagrams/README.md) | **Diagrams.** Mermaid source diagrams are versioned as documentation and should be updated with architecture/behavior changes. |
| [`SYSTEM_ARCHITECTURE.md`](Scrobblix-Docs/diagrams/SYSTEM_ARCHITECTURE.md) | **System Architecture Diagram.** Documentation for system architecture diagram. |
| [`TRUST_BOUNDARIES.md`](Scrobblix-Docs/diagrams/TRUST_BOUNDARIES.md) | **Trust Boundaries.** Documentation for trust boundaries. |
| [`UI_FLOW.md`](Scrobblix-Docs/diagrams/UI_FLOW.md) | **UI Flow.** Documentation for ui flow. |

## references

| File | Purpose |
|---|---|
| [`OFFICIAL_SOURCES.md`](Scrobblix-Docs/references/OFFICIAL_SOURCES.md) | **Official Sources.** Accessed 2026-08-01. Recheck before implementation/release because APIs, platform support, and terms can change. |
| [`README.md`](Scrobblix-Docs/references/README.md) | **References.** Documentation for references. |
| [`SOURCE_REVIEW_CHECKLIST.md`](Scrobblix-Docs/references/SOURCE_REVIEW_CHECKLIST.md) | **External Source Review Checklist.** When an external fact influences requirements or implementation: |
