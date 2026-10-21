# Consolidated Recommendations

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Recommended baseline  
> **Last updated:** 2026-08-01

This document collects the principal product and engineering recommendations that are otherwise distributed across the repository. Each recommendation should be converted into an Architecture Decision Record when implementation changes its status from proposed to accepted.

## Product positioning

Position Scrobblix as a **privacy-first Windows listening companion**, not merely another Last.fm submission utility. Reliable scrobbling is the entry point; the durable product value is transparent playback tracking, local history, diagnostics, and useful listening insights.

The core promise should remain:

1. What am I listening to?
2. What have I listened to?
3. Why did or did not this track scrobble?

Avoid building social feeds, recommendation engines, cloud accounts, or a full music player before the reliability foundation is proven.

## Production technology baseline

Use C#, .NET 10 LTS, WinUI 3, and Windows App SDK for the production Windows client. This provides the strongest fit for Windows media APIs, Fluent UI, notifications, packaging, lifecycle, accessibility, and credential storage. Keep the domain and application layers independent of WinUI and WinRT so tests remain fast and future front ends remain possible.

A Python/PySide6 implementation is acceptable for a disposable proof of concept, but it should not become an accidental production architecture.

## Playback detection

Use a hybrid strategy:

- GSMTC is the default, zero-install discovery path for compliant desktop and browser players.
- An optional Browser Companion provides precise tab/site/media-element metadata where GSMTC is missing, ambiguous, or stale.
- Site adapters are isolated and testable; generic HTML media detection is the fallback.
- Audio capture and acoustic fingerprinting remain out of scope because of privacy, resource use, recognition uncertainty, and scrobble-timing ambiguity.

Never equate “media session exists” with “track was listened to.” Only confirmed playing intervals count toward eligibility.

## Browser-native communication

Use browser Native Messaging for extension-to-native transport. Forward validated observations into the main app through a per-user, access-controlled IPC channel such as named pipes. Do not expose an unauthenticated localhost HTTP or WebSocket service.

The protocol must be versioned, bounded, schema-validated, replay-resistant where appropriate, and capable of rejecting unsupported extension or native-host versions with a user-readable remediation message.

## Scrobble correctness

Implement the timing and duplicate logic as deterministic pure domain code with an injected wall clock and monotonic clock. Persist the transition to an eligible queue item before attempting network delivery.

Model “now playing” and “scrobble” as separate actions. A failed now-playing update should not block or invalidate a later qualifying scrobble. Provider responses must be stored and mapped to stable reason codes rather than collapsed into generic success/failure states.

## Local-first data architecture

Use SQLite as the authoritative local store. Keep playback observations, canonical plays, state transitions, queue actions, delivery attempts, and user-visible reason codes auditable. Use write-ahead logging where testing confirms it is safe for the chosen deployment and backup model.

Store Last.fm session secrets in Windows Credential Locker, not in SQLite, logs, settings JSON, exports, crash reports, or support bundles.

## UI/UX priorities

Build the user interface in this order:

1. Tray status and current-playback flyout.
2. Home/live activity.
3. Queue.
4. Diagnostics.
5. History/timeline.
6. Settings and sources.
7. Statistics and loved tracks.

Diagnostics is a primary product surface, not a developer-only afterthought. Every skipped, delayed, duplicate, rejected, or failed action needs a reason, evidence, and the next useful action.

Use the system theme by default, respect reduced-motion and high-contrast settings, retain keyboard parity, and avoid relying on colour alone for status.

## Distribution and updates

Start with a signed MSIX/App Installer release and a portable/developer path only when maintenance capacity permits. Separate installer, update, migration, and rollback testing. Do not auto-update the browser companion or native host independently in ways that can silently break protocol compatibility.

Signing keys, store credentials, and release tokens must remain outside the repository and be protected as high-impact secrets.

## Open-source structure

Begin as a monorepo under `frpboy/scrobblix` so source, extension, packaging, tests, and documentation evolve atomically. Split repositories only when release cadence or contributor ownership creates a concrete need.

Require every behavior-changing pull request to update tests and relevant documentation. Use Architecture Decision Records for irreversible or expensive-to-reverse decisions.

## Privacy and telemetry

Make telemetry disabled by default. Keep local history, diagnostics, crash reporting, and product analytics as separate controls. A user must be able to use Scrobblix without creating a Scrobblix account or sending listening history to any service other than the provider they explicitly connect.

Support bundles must preview included files, redact secrets, minimize personal listening data, and require explicit export.

## Scope controls

Defer these until after v1 reliability targets are met:

- Cloud sync.
- Multi-device identity.
- AI-generated recommendations.
- macOS/Linux clients.
- Plugin marketplace.
- Audio fingerprinting.
- Mobile apps.
- Full remote control of media players.

## Brand and legal diligence

Perform product-name, domain, package-identifier, store-listing, and trademark clearance before a public launch. Do not assume availability from a GitHub repository search alone. Document third-party licences and comply with Last.fm attribution and API terms. Re-review the terms before commercialisation, sponsorship, or paid services.

## First implementation milestone

The first engineering milestone should be a headless vertical slice that can:

1. Accept synthetic observations.
2. Produce one canonical playback.
3. accumulate confirmed played time.
4. create exactly one eligible queue action.
5. survive process restart.
6. classify a simulated provider response.
7. explain every transition through reason codes.

Only after this is deterministic should live GSMTC, WinUI, browser integration, and release packaging be layered on top.
