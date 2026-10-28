# Backup and Restore — Current Implementation

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Source-backed ZIP backup; full recovery matrix still pending  
> **Last reviewed:** 2026-10-28

## Current backup format

The implementation is src/Scrobblix.Infrastructure/Storage/ScrobblixBackupService.cs. The versioned ZIP archive contains exactly two entries:

- manifest.json
- scrobblix.db

The manifest records:
- formatVersion (currently 1);
- schemaVersion (currently 5);
- createdUtc;
- databaseEntry;
- databaseBytes;
- sha256 (SHA-256 checksum of the database snapshot);
- appVersion.

The database snapshot is created through the SQLite backup API. The archive is written to a temporary file, flushed, size-checked and moved into its destination. Maximum database/archive sizes are bounded. Credentials/session keys, non-database preferences and cached artwork are not included in this backup format.

## Create a backup

Use the in-app backup flow where available and store the ZIP in a user-controlled location. Do not overwrite the active SQLite database path with the archive. Keep at least one copy separate from the device if this history matters to you; an archive on the same failing disk is not a resilient backup.

## Stage a restore

The current restore path:
1. Bounds the selected archive size and requires exactly the manifest and database entries.
2. Validates manifest format/schema/database entry metadata.
3. Extracts the database through a bounded copy into a staging directory.
4. Checks byte length and SHA-256 against the manifest.
5. Opens the staged database read-only, runs SQLite integrity_check, and verifies the expected migration chain.
6. Writes a staged manifest only after validation succeeds.

The active database is not replaced by the staging operation. A successful restore is applied during a later app launch after database writers have stopped.

## Applying a staged restore

The application snapshots the current database before replacement. It moves the existing database and SQLite WAL/SHM sidecars into a rollback set before activating the validated staged database. If a sidecar cannot be detached, the operation fails closed and attempts to put moved files back. If rollback itself fails, the exception identifies preserved rollback files; do not delete those files while recovering.

## Important limitations

- The archive contains the database only; it is not an export of preferences, logs, artwork, browser IDs or provider credentials.
- The restore currently expects the schema migration chain embedded in this build. Backups from an unknown/future schema are rejected rather than guessed at.
- Staged restore applies on app launch, not immediately while active services are using SQLite.
- Automatic migration rollback across arbitrary older application versions is not established; use a compatible app build or a known-good database backup.
- The corruption, disk-full, locked-sidecar, interrupted-process and rollback-failure matrix still needs real Windows qualification.

## Recovery checklist

Before attempting a restore, preserve the existing database and any rollback/safety-snapshot files. Never delete the only copy while diagnosing integrity or migration failure. After restoring, launch the app, inspect local history and queue state, and reconnect Last.fm if needed. Remote scrobbles already accepted by Last.fm are unaffected by restoring/deleting the local database.

Record backup/restore test results against the exact app SHA with disposable test data. Keep unexecuted cases NOT RUN.
