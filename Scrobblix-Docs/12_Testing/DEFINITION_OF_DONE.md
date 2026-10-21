# Definition of Done

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

A change is done only when:

- linked requirement/issue and intended behavior are clear;
- architecture boundaries are respected or ADR updated;
- production implementation has no intentional placeholders;
- relevant unit/integration/E2E tests pass;
- error/degraded/recovery paths are implemented;
- security/privacy/data/migration impacts reviewed;
- user-visible strings/accessibility are complete;
- diagnostics/reason codes/log redaction are complete;
- documentation and traceability updated;
- dependency/license changes reviewed;
- performance measured for hot-path changes;
- CI is green and reviewer concerns resolved.

Manual-only verification is recorded with environment/evidence. A feature hidden behind a flag still requires safe defaults, cleanup path, and documentation.
