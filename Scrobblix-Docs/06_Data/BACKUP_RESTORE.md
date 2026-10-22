# Backup and Restore

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Backup contents

- SQLite database consistent snapshot.
- Non-secret settings and exclusions.
- Manifest with product version, schema version, UTC creation time, checksums, and included components.
- Optional artwork cache excluded by default.
- Credential/session keys excluded by default.

Format: versioned ZIP container with JSON manifest and database snapshot. The extension uses a Scrobblix-specific extension such as `.scrobblix-backup` only if file association is useful.

## Backup procedure

Use SQLite backup API or a validated checkpoint/snapshot method. Write to temporary file, compute checksums, then atomically rename. Never copy an active database naively.

## Restore procedure

1. Validate archive path, size, manifest, checksums, schema, and zip-slip safety.
2. Show account/history date/count summary without importing.
3. Stop writers and create safety backup of current state.
4. Restore into temporary location.
5. Migrate and integrity-check temporary database.
6. Atomically switch and restart services.
7. Roll back on any failure.

## Portability

Backups may move between compatible Scrobblix installations for the same user. Credentials require reauthentication unless a future encrypted credential-export design is explicitly approved.


## Atomic restore replacement and sidecars

The restore path prepares and validates a replacement file before touching the active database. When applying it, Scrobblix moves the current database and any SQLite `-wal`/`-shm` sidecars to a tracked rollback set, then activates the replacement. If a sidecar cannot be detached (for example, a file lock), the operation fails closed and restores moved files to their original paths. If rollback itself is blocked, the exception names the preserved rollback files; do not delete them until recovery is complete. A validated pre-restore database snapshot is also retained.
