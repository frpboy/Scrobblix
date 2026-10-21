# Local Observability

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Metrics

Observation rate/drop/coalesce, active sessions/plays, eligibility latency, queue counts/oldest age/attempts, provider latency/classification, database latency/size, adapter completeness, extension reconnects, analytics lag, CPU/memory/startup.

## Local-only dashboard

Diagnostics may show metrics and export them in support bundle. No upload by default. Metrics use opaque IDs and bounded history.

## Correlation

Observation → canonical play → queue action → provider attempt shares correlation/causation links. Users can copy a short correlation ID for support without exposing metadata.
