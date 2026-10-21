# ADR-0005 — Use SQLite for local persistence

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Status

Accepted

## Context

Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery path.

## Decision

SQLite provides transactional embedded storage, indexes, migrations, backup APIs, and no service dependency. It stores history, queue, attempts, state transitions, and local read models. Secrets remain outside SQLite. The cost is careful migration/concurrency design and database recovery work.

## Consequences

- Implementation and documentation must follow this decision while it remains accepted.
- Reversal requires a superseding ADR, migration impact, and updated tests/requirements.
- Dependencies introduced by the decision are pinned and reviewed through the engineering process.
