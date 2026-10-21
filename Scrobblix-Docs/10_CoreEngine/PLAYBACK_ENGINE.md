# Playback Engine

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Inputs

Validated immutable observations from GSMTC and Browser Companion, system lifecycle events, settings/exclusions, and injected clocks.

## Active play identity

A play is initially keyed by source session/media identity then correlated by normalized artist/title, duration tolerance, playback timing, and source relation. A metadata change can be enrichment, correction, or new track; rules decide based on identity/timeline reset/evidence.

## Played-time accumulation

Open an interval only when canonical state is confidently `Playing`. Close on pause, stop, source uncertainty, app tracking pause, sleep, or replacement. Duration is monotonic elapsed time, optionally sanity-checked against position delta. Never add an unknown gap automatically.

## Handoffs

When extension/GSMTC or browser/desktop representations hand off the same playback, preserve canonical play and confirmed time if correlation confidence meets threshold. Record handoff transition and winning source.

## Terminalization

A play ends/reaches abandoned/ignored/duplicate due to reliable track change, session removal after grace, explicit stop, user exclusion, invalid metadata policy, or shutdown reconciliation. Terminal reason is required.

## Determinism

Given ordered observations and clock values, output transitions must be reproducible. Property tests cover reordering tolerance, duplicate events, clock jumps, and restart snapshots.
