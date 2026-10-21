# Error and Reason Taxonomy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Stable categories

- `source.*` — session unavailable, permission, adapter, stale observation.
- `metadata.*` — missing/invalid/ambiguous fields.
- `playback.*` — insufficient time, short track, unknown duration/state, seek/restart.
- `duplicate.*` — merged canonical play or duplicate provider action.
- `queue.*` — persistence, lease, cancelled, retry exhausted.
- `provider.auth.*` — not connected, invalid session.
- `provider.rate_limit.*`
- `provider.temporary.*`
- `provider.permanent.*`
- `provider.ignored.*`
- `storage.*`
- `extension.*`
- `update.*`
- `security.*`

## Example reason codes

`metadata.missing_artist`, `playback.track_too_short`, `playback.insufficient_confirmed_time`, `duplicate.matched_existing_play`, `provider.auth.invalid_session`, `provider.ignored.timestamp_too_old`, `extension.native_host_missing`, `storage.database_integrity_failed`.

## Error object

Contains code, severity, retryability, user-action requirement, user-message key, technical summary, correlation ID, source/provider codes, and optional recommended action IDs. Exceptions are not themselves the contract.

## Logging/UX

Reason codes are stable and searchable. User wording is localized and can change. Stack traces remain local technical detail and are never the only explanation.
