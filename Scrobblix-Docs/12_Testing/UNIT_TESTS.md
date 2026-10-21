# Unit and Property Tests

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Domain targets

- Eligibility threshold boundaries (30s, half, 4 minutes, rounding).
- Played-time intervals with pause/seek/sleep/clock jumps.
- State-machine transitions and invalid transitions.
- Metadata normalization and identity equality.
- Source arbitration/conflict/handoff.
- Duplicate keys and genuine repeat handling.
- Retry/backoff/jitter bounds and classifications.
- Reason-code mapping.
- Analytics calculations.

## Property tests

Examples: confirmed played time never decreases; paused time never increases it; eligibility fires at most once; one canonical play/provider creates at most one scrobble action; retry preserves original timestamp; event duplication does not change terminal outcome.

## Style

Tests use descriptive Given/When/Then names, fixed/injected clocks, synthetic metadata, no network or real time sleeps, and avoid testing implementation details where behavior suffices.
