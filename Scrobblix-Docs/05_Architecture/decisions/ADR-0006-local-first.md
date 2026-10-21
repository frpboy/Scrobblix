# ADR-0006 — Keep core operation local and backend-free

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Status

Accepted

## Context

Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery path.

## Decision

Detection, history, analytics, queue, diagnostics, and settings run on-device. No Scrobblix account or hosted backend is required. This minimizes privacy risk and operating cost. Features such as cloud sync or secret-signing broker require separate future decisions.

## Consequences

- Implementation and documentation must follow this decision while it remains accepted.
- Reversal requires a superseding ADR, migration impact, and updated tests/requirements.
- Dependencies introduced by the decision are pinned and reviewed through the engineering process.
