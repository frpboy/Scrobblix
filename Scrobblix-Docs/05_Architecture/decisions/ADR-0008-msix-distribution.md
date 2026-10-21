# ADR-0008 — Prefer signed MSIX and App Installer

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Status

Proposed

## Context

Scrobblix requires a durable decision for this cross-cutting concern. The decision must optimize reliability, security, native Windows integration, maintainability, and a realistic solo-maintainer delivery path.

## Decision

Primary public distribution should use a signed MSIX with App Installer/GitHub-hosted releases, followed by Store/WinGet as appropriate. It supports identity, clean install/update/uninstall, and security. Validate Native Messaging registration and tray/startup behavior before accepting. A portable build is later and may have reduced integration.

## Consequences

- Implementation and documentation must follow this decision while it remains accepted.
- Reversal requires a superseding ADR, migration impact, and updated tests/requirements.
- Dependencies introduced by the decision are pinned and reviewed through the engineering process.
