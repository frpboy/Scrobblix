# Health and Diagnostics Model

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

Each component implements named health checks returning status (`healthy`, `degraded`, `unhealthy`, `unknown`), checked time, stable reason, safe evidence, and repair actions.

## Checks

Application services, database/migrations/integrity, disk space, GSMTC/session events, extension/Native Host/version, provider auth/API/key/rate, network/DNS/TLS/clock, queue age/leases, analytics projection, notifications, updater/signature, cache.

## Scheduling

Cheap checks periodic/event-driven; expensive checks manual or low-frequency. Health checks cannot mutate data unless invoked as an explicit repair.

## UX

Aggregate overall state is the worst actionable core component, not a simple average. A yellow optional extension warning must not make local GSMTC-only mode appear broken when the user chose it.
