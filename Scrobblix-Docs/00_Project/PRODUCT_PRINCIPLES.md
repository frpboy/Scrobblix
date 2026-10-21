# Product Principles

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## 1. Reliability before decoration

Visual polish matters, but a scrobbler that loses events is defective. Eligibility, persistence, retry, recovery, and observability receive priority over secondary analytics or themes.

## 2. Explain every decision

The system must not expose only a green/red status. It must state whether a track is waiting, eligible, queued, submitted, ignored, duplicated, superseded, or failed and why.

## 3. Local first

Core detection, history, statistics, queueing, and diagnostics operate locally. The app remains useful when Last.fm is offline. Cloud services are not introduced merely for convenience.

## 4. Least privilege

The browser extension requests only permissions required for supported detection. The native bridge accepts messages only from allow-listed extension IDs and validates every payload.

## 5. No invisible audio surveillance

Scrobblix observes media metadata and playback state. It does not capture microphones, loopback audio, video frames, page text unrelated to playback, or general browsing history.

## 6. Determinism over heuristics

Playback state transitions, eligibility calculations, duplicate keys, retry classifications, and source selection must be testable pure logic wherever possible. Heuristics are isolated, measurable, and visible to the user.

## 7. Graceful degradation

When the extension is absent, GSMTC continues. When Last.fm is unavailable, local history and queue continue. When album art fails, metadata remains usable. Optional features must not destabilize core scrobbling.

## 8. Native but maintainable

Use Windows capabilities through explicit infrastructure adapters, keeping domain behavior independent of WinRT, UI, HTTP, and storage implementations.

## 9. User control

Users can pause detection, disable sources, exclude applications/sites, inspect or delete local data, export history, clear caches, and revoke Last.fm access.

## 10. Documentation is executable intent

Requirement IDs, state machines, schemas, error codes, test cases, and ADRs are part of the product. Code that diverges from them must update them in the same change.
