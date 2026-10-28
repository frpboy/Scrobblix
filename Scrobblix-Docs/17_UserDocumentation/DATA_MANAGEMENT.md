# Data Management — Current Capabilities vs Policy Targets

> **Audience:** Scrobblix users and testers  
> **Status:** Current preview behavior; data lifecycle features are not all implemented  
> **Last reviewed:** 2026-10-28

## History and exports

Scrobblix stores local playback observations, play records, provider actions and loved-track state in SQLite. The current documented history export is CSV. Do not rely on the older proposal for versioned JSON/date-range export unless that option is present in the exact release UI.

Local history and remote Last.fm history are separate. Deleting a local record, if/when available, does not delete a scrobble already accepted by Last.fm.

## Backup

The current source implements a versioned backup/restore path with archive/schema/integrity validation, staged restore and a pre-restore safety snapshot. API secrets/session keys and cached artwork are excluded from backup archives; reconnect provider credentials after a restore if needed.

Use a backup made by Scrobblix and preserve the original database before recovery. Test restore against disposable data before relying on a backup for irreplaceable history.

## Restore behavior

A safe restore path should validate archive structure/size, manifest and schema version, database integrity and migration compatibility before replacing active data; create a safety snapshot; stage writes; and recover the original state if replacement fails. The implementation has staging/safety behavior, but the entire corruption/sidecar/rollback matrix still requires real Windows qualification.

## Deletion and retention

The current source baseline does not establish a complete user-facing menu for deleting selected history ranges, wiping all data, choosing per-category retention periods, or purging all logs/cache. Do not follow this document as if all those controls exist in the current UI.

Before a future release advertises full deletion, define and test the scope for history, observations, provider actions (including ambiguous/pending actions), preferences, logs, artwork cache, browser registration, and credentials. The confirmation must explain that removing local data does not remove remote Last.fm records.

## Portability and privacy

Credentials are not exported. Listening history and diagnostics are sensitive even when stored on the user's own PC. The support bundle is a separate diagnostics-only export and is not a backup/history export.

For current data paths, see [Database Schema](../06_Data/DATABASE_SCHEMA.md), [Retention and Deletion](../06_Data/RETENTION_AND_DELETION.md), and [Support Bundle](../14_Operations/SUPPORT_BUNDLE.md).
