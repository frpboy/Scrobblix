# Source Arbitration

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Purpose

Merge reports of the same real playback and choose the most trustworthy fields/state.

## Correlation evidence

- normalized artist/title match;
- duration within tolerance;
- temporal overlap/start proximity;
- browser/application relationship;
- position proximity and progression;
- artwork/media IDs;
- source session linkage;
- adapter confidence.

## Priority

A verified site adapter generally outranks generic Media Session, which may outrank incomplete GSMTC for browser media. For desktop apps, GSMTC may be authoritative. Fresh reliable playback state can come from one source while richer metadata comes from another. Arbitration is field-aware, not simply “one source wins everything.”

## Scoring

Use documented weighted evidence with hard conflict rules. Scores/reasons are persisted for diagnostics. Do not expose arbitrary user weights in v1.

## Conflicts

If two active observations have different valid media identities, treat them as separate candidates and select active canonical focus using playing state, recent user/system current session, audible/visibility hints where reliable, and user source preference. Do not merge merely because timestamps overlap.

## Grace

Short disconnects permit continuation; grace windows are bounded and source-specific. Genuine replay of the same track after restart/position reset creates a new play once evidence passes replay rules.
