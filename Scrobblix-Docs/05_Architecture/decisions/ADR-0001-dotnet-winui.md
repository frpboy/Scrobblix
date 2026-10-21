# ADR-0001 — Use .NET 10 and WinUI 3

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Status

Accepted

## Context

Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery path.

## Decision

Scrobblix is Windows-first and depends on GSMTC, Credential Locker, notifications, packaging, lifecycle, and Fluent UI. Use C# on .NET 10 LTS with WinUI 3/Windows App SDK. Python/PySide6 remains suitable for experimentation but is not the production baseline. This increases native integration quality and long-term support while accepting Windows-only UI code and WinUI-specific testing complexity.

## Consequences

- Implementation and documentation must follow this decision while it remains accepted.
- Reversal requires a superseding ADR, migration impact, and updated tests/requirements.
- Dependencies introduced by the decision are pinned and reviewed through the engineering process.
