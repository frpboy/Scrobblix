# ADR-0002 — Use Clean Architecture boundaries

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Status

Accepted

## Context

Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery path.

## Decision

Separate Domain, Application, Infrastructure, and Presentation projects. The deterministic playback and queue rules must be testable without Windows, database, network, or UI. This adds project/contract overhead but prevents platform code from owning product rules.

## Consequences

- Implementation and documentation must follow this decision while it remains accepted.
- Reversal requires a superseding ADR, migration impact, and updated tests/requirements.
- Dependencies introduced by the decision are pinned and reviewed through the engineering process.
