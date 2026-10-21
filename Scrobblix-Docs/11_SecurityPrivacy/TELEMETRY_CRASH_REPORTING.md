# Telemetry and Crash Reporting

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Default

Telemetry is off. No listening history, page metadata, or crash dump is uploaded automatically.

## Local metrics

Health/reliability metrics may be calculated and displayed locally. They remain in the user's database/logs under retention controls.

## Future opt-in telemetry

Requires a separate data map, purpose limitation, schema, retention, endpoint/security, consent UX, deletion mechanism, privacy update, and ADR. Prefer aggregate counters that cannot reconstruct listening history. Do not use advertising identifiers or cross-product tracking.

## Crashes

Capture local crash metadata/dumps only when platform configuration and privacy allow. Dumps can contain secrets/history, so support bundle excludes raw dumps by default or clearly warns/previews. Automatic third-party crash SDKs are not introduced casually.
