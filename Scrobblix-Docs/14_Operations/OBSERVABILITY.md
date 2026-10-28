# Local Diagnostics and Observability

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Current local diagnostics inventory; no remote telemetry service  
> **Last reviewed:** 2026-10-28

## Current observability surfaces

The current application uses:
- A local file logger with bounded, structured event categories and redaction for sensitive values/listening metadata.
- Diagnostics and source/provider status in the desktop UI.
- Queue state/reason codes for provider actions, including authentication, retry, ignored, permanent-failure and ambiguous-outcome states.
- Browser Companion connection diagnostics invoked explicitly from its popup.
- A user-selected, diagnostics-only support ZIP with a versioned manifest, event summary, sanitized operational-events TSV and README.

There is no remote Scrobblix telemetry backend, automatic support upload, or full remote performance dashboard in the current implementation. Do not describe planned metrics as if already collected or presented to users.

## Signals currently available or derivable locally

Operational diagnosis can use local events and current state for:
- playback-source status and last observation;
- current track/playback state and local eligibility reason;
- provider action counts/state/reason and retry due time;
- storage initialization and migration/integrity failures;
- Last.fm authorization/provider results;
- native-host connection diagnostic categories;
- app startup/shutdown and selected notification/tray/logging failures.

Process CPU/memory history, event-rate/drop/coalesce counters, a complete per-attempt provider latency series, analytics lag, universal source health metrics, and a historical resource dashboard are not claimed as shipped merely because they are useful future signals.

## Correlation and privacy

Use local action/play identifiers only where necessary to connect observation → canonical play → provider action. Correlation must not expose artist/title, full browser URLs, credentials or listening metadata in ordinary diagnostic messages. A user-visible support export contains normalized source kind/state/last-observation time/tracking enabled plus allow-listed operational events; it must not include the database or raw logs.

## Support bundle

The current export is user-initiated and local. It previews/asks for confirmation, writes the ZIP atomically, shows a SHA-256 digest, and does not upload it. Real Windows Save/cancel, redaction canaries, archive contents and accessibility are still qualification tasks.

## Reliability measurements still needed

During an 8–24 hour real playback soak, separately measure process CPU/memory, SQLite size, log growth, UI responsiveness, source changes, queue state and provider acceptance against a dedicated Last.fm test profile. Capture start/end samples and sanitized errors. The current CI workflow does not do this soak or certify live media/provider correctness.
