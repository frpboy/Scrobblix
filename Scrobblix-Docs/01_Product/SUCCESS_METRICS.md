# Success Metrics

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


Metrics are measured locally during development/test unless users explicitly opt into telemetry. Public product claims must be based on a documented test corpus, not inferred from opt-in production data alone.

## Reliability targets

| Metric | v1 target |
|---|---:|
| Eligible play persisted before submission | 100% in automated fault-injection tests |
| Eligible plays eventually accepted after transient outage | >= 99.9% in controlled recovery tests |
| Duplicate remote submissions in correlation corpus | < 0.1% |
| False scrobble of known advertisement/non-music fixture | 0 in required adapter corpus |
| Queue recovery after forced termination | 100% |
| Database migration data retention | 100% for supported upgrade paths |
| Diagnostic reason coverage for terminal play states | 100% |

## Performance targets

| Metric | Target on reference hardware |
|---|---:|
| Idle CPU | <= 0.5% average over 15 minutes |
| Active tracking CPU | <= 2% average excluding debug logging |
| Idle private working set | <= 150 MB goal; <= 220 MB hard release threshold |
| Cold launch to usable tray state | <= 2.5 seconds p95 |
| Home current-track update after source event | <= 500 ms p95 |
| Extension-to-desktop observation latency | <= 250 ms p95 locally |
| Common history search over 100k records | <= 300 ms p95 |

## UX targets

- First-run Last.fm connection completion >= 90% in moderated tests.
- Representative missed-scrobble diagnosis completed in <= 2 minutes without raw-log inspection.
- All primary flows keyboard-completable.
- No critical accessibility violations in automated scans and manual screen-reader pass.

## Product health indicators

- Crash-free sessions.
- Authentication failure rate.
- Queue age and retry distribution.
- Source-adapter metadata completeness.
- Percentage of detected plays becoming eligible.
- Percentage of eligible plays accepted/ignored by provider.
- Support reports resolved through built-in diagnostics.

These indicators are visible locally even when telemetry remains disabled.
