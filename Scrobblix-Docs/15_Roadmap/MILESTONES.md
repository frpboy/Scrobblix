# Milestones

> **Project:** Scrobblix  
> **Status:** Private-beta readiness; implementation exists, release evidence is incomplete  
> **Updated:** 2026-10-22  
> **Integration baseline:** `main`

This plan separates **implemented in the codebase** from **verified for release**. A surface or code path being present is not evidence that it survives long sessions, network/provider failures, browser lifecycle changes, install/upgrade, or real user data.

## M0 — Engineering foundation — implementation present

Solution structure, domain model, SQLite storage/migrations, browser companion projects, Windows CI and local test tooling exist. Validate CI against the exact commit being considered; old successful runs do not validate newer commits.

## M1 — Reliable local tracker — implementation present; acceptance pending

GSMTC discovery, canonical playback tracking, eligibility, local history, Home, History search/export and diagnostics are implemented. Remaining acceptance includes repeat/seek/pause semantics, duplicate prevention, source arbitration, sleep/wake, crash/restart and long-run stability on real Spotify sessions.

## M2 — Last.fm delivery — implementation present; live-account acceptance pending

Desktop authorization, Credential Locker boundary, Now Playing, scrobble, Love/Unlove actions and a durable local queue exist. Verify the exact provider outcome against a dedicated test account, including ambiguous network failures and repeated-track idempotency. Never infer provider acceptance from a local queue state alone.

## M3 — Optional Browser Companion — development integration present; browser qualification pending

Chromium and Firefox development manifests, supported-site observers, Native Messaging host and registration/uninstall helpers exist. GSMTC remains the preferred path for Spotify desktop; the companion is an optional precision source. Test Chrome, Edge and Firefox separately with actual extension/add-on IDs, browser restarts, reconnects and hostile/invalid messages.

## M4 — Everyday Windows experience — core surfaces implemented; usability/reliability acceptance pending

Home, History, Statistics, Loved, Queue, Sources, Diagnostics, Settings, backup/restore paths, tray behavior and opt-in notifications are present in the codebase. Validate each interaction on real Windows. Explorer/taskbar restart recovery, first-run onboarding, source-health clarity, support-bundle generation and accessibility must not be considered finished without evidence.

## M5 — Private beta readiness — current milestone

1. Reconcile roadmap and release docs with the code that exists on `main`.
2. Run the manual real-world matrix over several days with Spotify desktop and a dedicated Last.fm test account; record actual results instead of assuming success.
3. Complete an 8–24 hour soak and fault/restart recovery pass.
4. Qualify backup/restore on a disposable copy of real test data.
5. Finish Native Messaging/security review and implement/test privacy-safe support export.
6. Improve user-facing tracking/eligibility/delivery wording and Sources diagnostics.
7. Review accessibility, privacy, dependency inventory, known issues and beta distribution constraints.

## M6 — Installable release candidate

Build a reproducible MSIX/App Installer path; obtain the publisher identity/certificate; sign and timestamp packages; test clean install, upgrade, repair, rollback and uninstall on a clean Windows account/device. Add update policy, provenance, dependency/license inventory and SBOM. Signing cannot be marked complete until the maintainer supplies the required identity/certificate or chooses the Microsoft Store path.

## M7 — Public beta / v1

Publish only after M5/M6 acceptance evidence is reviewed. Release notes, known issues, privacy text, supported Windows/browser matrix, data-retention/removal behavior and support instructions must match the tested package.

## Explicitly deferred until after v1

Cloud sync, Discord, provider plugins, scripting/rules, ListenBrainz/Libre.fm, mobile/macOS/Linux clients, and a Spotify Web API dependency. These are not substitutes for proving correctness and installability of the Windows app.
