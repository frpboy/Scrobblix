# System Architecture

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Implementation-backed snapshot; planned release capabilities are called out separately  
> **Last reviewed:** 2026-10-28

## Purpose and source of truth

This document describes the current repository structure and boundaries visible in source. The solution files, project files, SQL migrations, and CI workflow are the implementation sources of truth. UI mocks and broad product specifications describe intended behavior; they are not evidence that every planned capability is shipped.

## Current component map

| Component | Current responsibility | Implementation location |
|---|---|---|
| Domain | Playback state, canonical-play tracking, eligibility, metadata identity, queue action lifecycle, reason codes | src/Scrobblix.Domain |
| Application | Playback observation ingestion, source arbitration orchestration, queue processing, presentation/read models, CSV export | src/Scrobblix.Application |
| Infrastructure | SQLite persistence, migration/checksum validation, Last.fm API, request signing, credential abstraction, metadata enrichment, backup/restore and diagnostics export | src/Scrobblix.Infrastructure |
| Windows integration | Global System Media Transport Controls (GSMTC) observation source and Windows credential storage | src/Scrobblix.Windows |
| Desktop UI | WinUI 3 window, settings and pages, local logging, notification-area tray, opt-in Windows notifications | src/Scrobblix.Desktop |
| Native Messaging host | Browser Native Messaging framing, extension identity allow-list, and forwarding to the desktop named pipe | src/Scrobblix.NativeHost |
| Browser Companion | TypeScript Manifest V3 extension for Chromium and a Firefox development manifest | browser-extension |
| Tests | Domain and infrastructure assertions plus browser, manifest, desktop UI, workflow and PowerShell checks | tests and scripts |
| Release tooling | Windows CI build and unsigned test-bundle assembly; GitHub preview-release workflow | .github/workflows and scripts |

The solution uses separate domain, application, infrastructure, Windows, desktop, and native-host projects. Do not collapse these into a single-project architecture diagram or assume all external integrations live in the UI project.

## Runtime and trust boundaries

1. A Windows media session is queried through GSMTC. The observation source attaches a fresh collector instance ID and sequence to observations. Artwork is cached under the user's local Scrobblix data directory and subject to provider/source policy.
2. Optional browser site adapters send metadata through the extension service worker and browser Native Messaging protocol. The extension is not a Last.fm client and does not receive Last.fm credentials.
3. The Native Messaging host validates the envelope and the caller's registered extension ID before dispatching a supported message. Frames are length-prefixed and capped at 256 KiB.
4. The native host forwards the observation over the per-user named pipe named scrobblix.browser.v1 by default. The pipe is current-user-only. The desktop adds checks for the client image path and the parent browser process.
5. The desktop/application ingestion pipeline validates and arbitrates observations, updates canonical play state, and writes local history and durable provider actions to SQLite.
6. The provider action processor calls Last.fm over HTTPS. The desktop stores the session key through the Windows Credential Locker abstraction. Provider outcomes and reason codes drive user-visible queue status.

The named-pipe peer checks are defense-in-depth. They are not a cryptographic challenge or proof that an executable was signed by the expected publisher. Same-user file/registry tampering and signed-distribution provenance remain security/release concerns.

## Data and persistence boundaries

SQLite is the local persistence store. The migration-backed physical tables are currently:

- schema_migrations
- playback_observations
- plays
- provider_actions
- accounts
- loved_tracks

See the implementation-backed [Database Schema](../06_Data/DATABASE_SCHEMA.md). Do not describe conceptual entities such as source instances, transition history, provider-attempt history, settings, or analytics projections as existing SQLite tables unless a migration actually creates them. Some ideas in the broader architecture baseline remain design proposals.

The user's Last.fm session key is stored via Windows Credential Locker. It must not be written to SQLite, logs, exported bundles, browser extension storage, or repository configuration. Backup archives must exclude credentials and unrelated caches.

## Last.fm integration

Last.fm calls are isolated behind the provider interface and infrastructure implementation. Current supported operations include authorization token/session exchange, scrobble submission, Now Playing, Love, and Unlove. Request signing follows Last.fm's API signing protocol. It uses MD5 because that is part of the legacy provider protocol; it is not used as a password-storage or general-purpose security hash.

An HTTP success code alone is not proof of an accepted scrobble. Parse provider responses and preserve explicit accepted, ignored, authentication, retryable, permanent-failure, and ambiguous-outcome states. Do not blindly resend an outcome-unknown scrobble without a deliberate duplicate-handling policy.

## Browser integration and native messaging

The browser companion is optional when GSMTC supplies adequate metadata. It improves source/tab precision and browser-specific observation delivery; it is not required for every desktop music-player scenario.

The host reads the per-user Chromium and Firefox extension allow-lists from the Scrobblix NativeMessaging registry configuration. Chromium origins are matched against the registered extension ID. Firefox uses the registered add-on ID. Identity validation occurs before message types, including ping, are handled. Unsupported messages and untrusted identities fail closed.

There is no general unauthenticated localhost HTTP/WebSocket endpoint in the current design. Do not add one as a shortcut around Native Messaging without a new threat assessment and architecture decision.

## Build, release, and validation reality

The CI workflow currently runs on Windows and performs solution restore/build, assertion tests, browser type checking/build, security and manifest checks, PowerShell parsing, and publishing/assembly of development test bundles. The documentation validator is added by the companion CI change and checks Markdown structure and repository-relative link targets.

The current published workflow produces unsigned development ZIPs and GitHub preview releases. It does not currently establish that a signed MSIX/App Installer, automatic updater, store publication, or production-signing pipeline exists. Do not describe those as implemented.

CI does not prove real Spotify session correctness, the live Last.fm account flow, real-browser installation/identity registration, Windows accessibility, long-duration reliability, install/upgrade/rollback/uninstall, or privacy behavior on a user's machine. Track these in the [real-world manual test matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md) and leave unexecuted checks as NOT RUN.

## Architectural constraints

- Keep domain logic independent of WinUI, WinRT, HTTP, SQLite APIs, and process-specific details.
- Validate and bound external observations before they reach the canonical-play state machine.
- Persist provider actions with a stable local idempotency key and use leases to avoid concurrent processing.
- Distinguish local detection/history from remote Last.fm acceptance.
- Do not leak listening data, API credentials, session keys, or raw logs through diagnostics.
- Update the corresponding requirements, migration/interface docs, security review, and test matrix whenever implementation behavior changes.
