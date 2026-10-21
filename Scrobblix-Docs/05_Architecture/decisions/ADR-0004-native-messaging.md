# ADR-0004 — Use browser Native Messaging

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Status

Accepted

## Context

Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery path.

## Decision

Use browser-native extension-to-app messaging rather than a general localhost WebSocket/HTTP server. Native Messaging provides browser-controlled origin allow-listing and no listening network port. The desktop installer registers host manifests; a per-user authenticated named pipe can connect the host process to the single main app instance.

## Consequences

- Implementation and documentation must follow this decision while it remains accepted.
- Reversal requires a superseding ADR, migration impact, and updated tests/requirements.
- Dependencies introduced by the decision are pinned and reviewed through the engineering process.
