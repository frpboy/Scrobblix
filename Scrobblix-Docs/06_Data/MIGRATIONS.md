# Database Migrations — Current Chain and Recovery Rules

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Implementation-backed migration snapshot  
> **Last reviewed:** 2026-10-28

## Current migration chain

The physical migration resources embedded under src/Scrobblix.Infrastructure/Storage/Migrations and registered in SqliteDatabase.cs are:

1. 0001_initial
2. 0002_accounts
3. 0003_local_history_artwork
4. 0004_richer_metadata
5. 0005_loved_tracks

The current backup format identifies SQLite schema version 5. Check the current code/embedded resources before changing this list.

## Migration history record

The current physical schema_migrations table stores:
- migration_id (primary key);
- checksum (required SHA-256 checksum of migration text);
- applied_utc (required UTC timestamp).

Do not document fields such as app version, semantic name, migration start/completion time, or result status as stored unless a future migration/code change adds them.

## Immutability and validation

The app applies migrations in order before creating normal storage/background components. Startup validates the known migration prefix and compares each stored checksum with the embedded migration text. Unknown future migrations, missing/gapped history or checksum mismatches fail initialization rather than silently using an incompatible schema.

A migration that has been applied to any user's database is immutable. Make corrections with a new numbered migration and keep registration/resource order, database schema and tests aligned.

## Procedure for a schema change

1. Inspect the existing table/index/foreign-key/constraint and every storage query.
2. Add the next immutable embedded SQL migration and register it in the compiled migration sequence.
3. Make the application code compatible with the migration and any supported old schema.
4. Add tests for fresh initialization, upgrade from the previous schema, uniqueness/foreign keys, failures and recovery.
5. Update Database Schema, Data Architecture, backup/restore and privacy docs if relevant.
6. Run the current Windows CI workflow for the exact candidate and manual backup/restore tests using disposable data.

## Backup and migration interaction

The backup archive currently declares schema version 5 and includes the database snapshot plus manifest only. Restore staging checks the schema/migration chain and SQLite integrity before applying on the next app launch; the current database and SQLite sidecars are preserved in rollback/safety recovery handling. Do not manually rewrite a live DB or delete rollback files while recovery is unresolved.

## Verification limitations

Automated assertions and CI do not establish that every real-world upgrade/sidecar-lock/disk-full case has passed. Leave those manual acceptance rows as NOT RUN until performed and recorded against the exact candidate SHA.
