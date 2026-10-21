# Analytics Engine

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Inputs

Committed canonical play changes/deletions and projector version. Provider status updates affect accepted/reliability projections without altering confirmed listening time.

## Processing

Incremental ordered projection into daily/entity/session/funnel tables. At-least-once event processing is made idempotent using play version/projector checkpoints.

## Rebuild

On schema/logic version change, build new projection tables or clear/recompute safely in background. Core tracking and history continue. Progress and cancellation are visible.

## Calculations

Listening time from confirmed milliseconds; play count according to selected basis; unique entities from normalized identity; sessions separated by inactivity threshold; top lists with deterministic tie ordering.

## Constraints

No remote analytics API, no model training, and no genre inference from private history without explicit future design. Genre charts require a trustworthy data source and terms review, so they are not v1 Must.
