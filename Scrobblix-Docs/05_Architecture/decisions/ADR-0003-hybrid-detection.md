# ADR-0003 — Use GSMTC plus optional Browser Companion

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Status

Accepted

## Context

Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery path.

## Decision

GSMTC provides broad low-overhead coverage without extension installation, while browser adapters provide exact tab/site metadata when system exposure is incomplete. The core must merge both. Audio fingerprinting is excluded because of privacy, CPU, service dependency, and poor playback-position semantics.

## Consequences

- Implementation and documentation must follow this decision while it remains accepted.
- Reversal requires a superseding ADR, migration impact, and updated tests/requirements.
- Dependencies introduced by the decision are pinned and reviewed through the engineering process.
