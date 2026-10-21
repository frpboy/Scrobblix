# Database Migrations

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Policy

Migrations are ordered, immutable after release, and applied before normal services start. A released migration file is never edited; corrections use a new migration.

## Migration record

Store migration ID, semantic name, app version, checksum, started/completed UTC, and result. A checksum mismatch for an already-applied migration is a critical diagnostic condition.

## Procedure

1. Acquire single-instance and migration lock.
2. Verify available disk and database quick-check.
3. Create pre-migration backup for destructive/large transformations.
4. Apply migrations in one transaction where SQLite permits.
5. Run post-migration validation and required data backfills.
6. Update schema version.
7. Start background services only after success.

## Failure behavior

- Roll back transactional changes.
- Preserve original database and backup.
- Enter recovery UI instead of repeatedly attempting destructive migration.
- Offer retry, open log, restore backup, or read-only export.

## Testing

Every supported upgrade path is tested with representative data volumes, malformed legacy rows, interrupted migration simulation, disk-full injection, and downgrade behavior. Downgrades are generally unsupported; users can restore a compatible backup or export/import through a documented format.
