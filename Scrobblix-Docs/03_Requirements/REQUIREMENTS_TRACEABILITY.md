# Requirements Traceability

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


This matrix is seeded for implementation. Each pull request adds concrete code/test references rather than deleting requirement links.

| Requirement group | Architecture/component | Primary tests | UX/document |
|---|---|---|---|
| FR-A lifecycle | Desktop host, lifecycle coordinator | Lifecycle integration suite | Onboarding, Settings |
| FR-B Last.fm | Signed `LastFmScrobbleProvider` boundary and Windows Credential Locker; auth coordinator/sync worker pending | Provider contract tests pending | Account settings, Queue |
| FR-C GSMTC | `GsmTcPlaybackObservationSource` startup snapshot adapter; event monitoring pending | WinRT integration fixtures pending | Sources, Diagnostics |
| FR-D browser | MV3 companion/media observer and strict Native Messaging host; secure desktop pipe pending | TypeScript check/build; Browser E2E/protocol fuzz pending | Browser Companion, Sources |
| FR-E playback | Eligibility, confirmed-time, canonical-state, safe metadata normalization, and durable raw-observation persistence slices | `tests/Scrobblix.Domain.Tests` eligibility/timing/state/metadata/storage self-check | Home progress, History detail |
| FR-F queue | Idempotency key and queue-action lifecycle initial domain slices; repository/scheduler pending | `tests/Scrobblix.Domain.Tests` idempotency/lifecycle boundary self-check | Queue page |
| FR-G history | History record/observation slices and SQLite observation repository; canonical-play repository/read model pending | `tests/Scrobblix.Domain.Tests` durable observation and duplicate-key self-check | History, Statistics |
| FR-H UI | WinUI desktop startup shell with visible ready/error state; navigation/tray pending | Build verification; UI automation/accessibility tests pending | Screen specifications |
| FR-I diagnostics | Health services, support bundle | Redaction/recovery tests | Diagnostics |
| NFR-P performance | All hot paths | Benchmark suite | Performance budget |
| NFR-S security | Credential/update/protocol boundaries | Security tests | Threat model |
| NFR-PR privacy | Storage/extension/logging/export | Privacy regression tests | Privacy notice |

## Traceability rule

A Must requirement cannot be marked complete until:

1. implementation reference exists;
2. automated test evidence exists, or a documented manual-only reason exists;
3. user-facing documentation is updated;
4. monitoring/diagnostic behavior is defined.
