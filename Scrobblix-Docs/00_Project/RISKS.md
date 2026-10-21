# Risk Register

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


| ID | Risk | Likelihood | Impact | Mitigation |
|---|---|---:|---:|---|
| R-001 | GSMTC metadata is absent or inaccurate for a player/site | High | High | Browser companion, adapter diagnostics, user exclusions, metadata completeness scoring |
| R-002 | Duplicate scrobbles from GSMTC and extension | High | High | Canonical-play arbitration, deterministic correlation window, unique submission key |
| R-003 | Last.fm changes API terms, access, or rate limits | Medium | High | Source register, release-time review, provider abstraction, queue backoff, non-commercial positioning |
| R-004 | Application API secret is extracted from desktop binaries | High | Medium | Dedicated key, no user secrets, obfuscation only as friction, monitoring, revocation plan, optional future signing broker ADR |
| R-005 | Native Messaging registration breaks after install/update | Medium | High | Installer tests per browser, health check, repair action, signed host path verification |
| R-006 | Browser-store rejection due to permissions | Medium | High | Minimal optional host permissions, per-site enablement, explicit privacy disclosure |
| R-007 | Database corruption loses local history/queue | Low | Critical | WAL mode, transactions, integrity checks, backups, migration tests, recovery export |
| R-008 | Sleep/resume corrupts playback timing | High | High | Monotonic elapsed-time model, resume reconciliation, explicit unknown intervals |
| R-009 | User assumes local history equals Last.fm history | Medium | Medium | Distinct local/provider statuses and filters; sync reconciliation UX |
| R-010 | Artwork caching violates provider/site terms or grows unbounded | Medium | Medium | Source attribution, cache budget, expiry, no redistribution, user clear-cache control |
| R-011 | WinUI/platform regression affects background/tray behavior | Medium | Medium | Pin versions, platform integration tests, canary release, fallback tray strategy |
| R-012 | Solo-maintainer workload causes over-scoped v1 | High | High | Milestone gates, strict MVP, defer plugin/cloud/AI features |
| R-013 | Name conflicts with an existing product or trademark | Low/Unknown | High | Perform formal repository, domain, store, and trademark clearance before branding investment |
| R-014 | Diagnostic bundles expose personal data | Medium | High | Preview/redaction, explicit consent, minimal defaults, automated secret scanning |
| R-015 | Site DOM changes break browser adapter | High | Medium | Adapter isolation, fixtures, remote compatibility notices only if privacy-preserving, fast patch cadence |
