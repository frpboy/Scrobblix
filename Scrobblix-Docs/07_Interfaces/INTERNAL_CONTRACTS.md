# Internal Application Contracts — Current Project Boundaries

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Source-backed interface catalogue  
> **Last reviewed:** 2026-10-28

## Source of truth

Actual contracts live in `src/Scrobblix.Application/Abstractions`, with concrete SQLite/Windows/provider implementations in Infrastructure, Windows and Desktop projects. This document intentionally describes the implemented interface families rather than reproducing outdated, non-compiling interface declarations from an earlier architecture proposal.

## Implemented application abstractions

| Contract | Purpose |
|---|---|
| `IPlaybackObservationSource` | Reads a current structured playback observation from a source adapter. |
| `IPlaybackObservationStore` | Persists observations for local history/audit and source reconciliation. |
| `ILocalPlayStore` | Persists canonical local-play state and eligibility/tracking results. |
| `IEligiblePlayStore` | Creates durable provider actions when an eligible play is first observed. |
| `IProviderActionStore` | Claims, leases, completes, retries, cancels, and recovers durable provider actions. |
| `IProviderAccountStore` | Reads and updates provider/account connection state without storing the session key in SQLite. |
| `ICredentialStore` | Retrieves, stores and deletes opaque credential values through the configured platform store. |
| `IWallClock` | Supplies UTC wall-clock time for timestamps. |
| `IMonotonicClock` | Provides elapsed-time measurements for tracking duration without trusting wall-clock changes. |
| `IEventLogger` | Emits structured events with privacy-redacted operational context. |

Use the exact existing declaration and its signatures before implementing a new adapter. Do not assume the repository includes a batch-oriented `IScrobbleProvider`, generic analytics repository, full provider-health API, or ORM-like repository abstraction unless a source file defines and tests it.

## Dependency direction

- Domain models and policies are independent of WinUI and concrete SQLite/WinRT APIs.
- Application services coordinate domain decisions and depend on purpose-specific interfaces.
- Infrastructure implements durable storage, Last.fm protocol/signing, metadata lookups, backup/restore and diagnostic export.
- Windows-specific source/credential capabilities are isolated behind adapter projects.
- Desktop presentation owns native UI, user settings interactions, event wiring and UI status; it should not own request-signing algorithms or build ad-hoc SQL commands for screen rendering.
- The Native Messaging host and browser extension are separately trusted boundaries and must not receive Last.fm session credentials.

## Contract change checklist

When changing an interface:
1. Inspect every implementation and caller before editing the contract.
2. Make cancellation and error outcomes explicit.
3. Preserve bounded inputs and stable reason codes.
4. Add regression assertions for failure/cancellation/recovery and provider/action-state transitions.
5. Update architecture, relevant interface/schema docs and the manual matrix if external behavior changes.
6. Run current Windows CI against the exact candidate SHA.

Avoid pasting old design-only interfaces into new implementation code; use the current contract files as the authority.
