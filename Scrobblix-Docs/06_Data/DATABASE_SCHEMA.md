# Database Schema

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Implementation-backed schema snapshot  
> **Last reviewed:** 2026-10-28

## Source of truth

The physical schema is created by the embedded SQL migrations in src/Scrobblix.Infrastructure/Storage/Migrations and registered in SqliteDatabase.cs. The current migration sequence is 0001_initial, 0002_accounts, 0003_local_history_artwork, 0004_richer_metadata, and 0005_loved_tracks.

SqliteDatabase validates that the applied migration history is a known prefix and compares the stored SHA-256 checksum with each embedded migration. **Do not edit a migration that may already have been applied.** Add a new numbered migration, update the schema documentation, and add migration/data assertions with the change.

The tables below are the current migration-backed SQLite schema, not the full conceptual data model in the product design documents.

## Physical tables

### schema_migrations

| Column | Type | Nullability / constraint | Purpose |
|---|---|---|---|
| migration_id | TEXT | Primary key | Ordered migration identifier |
| checksum | TEXT | NOT NULL | SHA-256 digest of the embedded migration text |
| applied_utc | TEXT | NOT NULL | UTC timestamp when migration was applied |

### playback_observations

| Column | Type | Nullability / constraint |
|---|---|---|
| observation_id | TEXT | Primary key |
| source_instance_id | TEXT | NOT NULL |
| session_id | TEXT | NULL allowed |
| sequence_number | INTEGER | NOT NULL |
| event_id | TEXT | NULL allowed |
| captured_utc | TEXT | NOT NULL |
| observed_utc | TEXT | NOT NULL |
| source_name | TEXT | NOT NULL |
| application_id | TEXT | NULL allowed |
| artist, title, album | TEXT | NULL allowed |
| duration_ms, position_ms | INTEGER | NULL allowed |
| playback_state | INTEGER | NOT NULL |
| normalized_artist, normalized_title | TEXT | NULL allowed |
| artwork_path, artwork_source | TEXT | Added by migrations 0003/0004; NULL allowed |
| album_artist | TEXT | Added by migration 0004; NULL allowed |
| track_number, release_year | INTEGER | Added by migration 0004; NULL allowed |
| recording_mbid, release_mbid | TEXT | Added by migration 0004; NULL allowed |

A unique constraint covers source_instance_id, session_id, sequence_number, and event_id. An index supports source/session/time lookups. SQLite treats NULL values as distinct in ordinary UNIQUE constraints; callers must provide an idempotent identity for repeated observations rather than assuming nullable identity components automatically deduplicate.

### plays

| Column | Type | Nullability / constraint |
|---|---|---|
| play_id | TEXT | Primary key |
| started_utc | TEXT | NOT NULL |
| ended_utc | TEXT | NULL allowed |
| artist, title, album | TEXT | NULL allowed |
| duration_ms | INTEGER | NULL allowed |
| confirmed_played_ms | INTEGER | NOT NULL |
| eligibility_threshold_ms | INTEGER | NULL allowed |
| eligible_utc | TEXT | NULL allowed |
| local_status | INTEGER | NOT NULL |
| terminal_reason_code | TEXT | NULL allowed |
| created_utc, updated_utc | TEXT | NOT NULL |
| artwork_path, artwork_source | TEXT | Added by migrations 0003/0004; NULL allowed |
| album_artist | TEXT | Added by migration 0004; NULL allowed |
| track_number, release_year | INTEGER | Added by migration 0004; NULL allowed |
| recording_mbid, release_mbid | TEXT | Added by migration 0004; NULL allowed |

An index supports descending started_utc history queries. Domain enum values stored in INTEGER columns must be changed compatibly with the application, migration rules, and existing local data.

### provider_actions

| Column | Type | Nullability / constraint |
|---|---|---|
| action_id | TEXT | Primary key |
| play_id | TEXT | NULL allowed; references plays(play_id) |
| account_id | TEXT | NOT NULL |
| provider | TEXT | NOT NULL |
| action_type | INTEGER | NOT NULL |
| state | INTEGER | NOT NULL |
| idempotency_key | TEXT | NOT NULL, UNIQUE |
| attempt_count | INTEGER | NOT NULL, defaults to 0 |
| due_utc | TEXT | NOT NULL |
| lease_owner, lease_expires_utc | TEXT | NULL allowed |
| last_reason_code | TEXT | NULL allowed |
| created_utc, updated_utc | TEXT | NOT NULL |
| completed_utc | TEXT | NULL allowed |
| track_key | TEXT | Added by migration 0005; NULL allowed |

An index supports provider/account/state/due-time selection, and a later index supports track-key history. Treat leases and idempotency as concurrency/recovery safeguards; they do not prove that an ambiguous remote request was accepted or rejected.

### accounts

Created by migration 0002.

| Column | Type | Nullability / constraint |
|---|---|---|
| account_id | TEXT | Primary key |
| provider | TEXT | NOT NULL |
| remote_username | TEXT | NOT NULL |
| credential_resource | TEXT | NOT NULL; reference/name, not the session key |
| is_connected | INTEGER | NOT NULL |
| connected_utc, last_verified_utc | TEXT | NULL allowed |
| created_utc, updated_utc | TEXT | NOT NULL |

A unique constraint covers provider and remote_username. The connected flag is stored state; it is not by itself proof that current credentials remain valid.

### loved_tracks

Created by migration 0005.

| Column | Type | Nullability / constraint |
|---|---|---|
| track_key | TEXT | Primary key |
| source_play_id | TEXT | NOT NULL; references plays(play_id) |
| artist, title | TEXT | NOT NULL |
| album | TEXT | NULL allowed |
| is_loved | INTEGER | NOT NULL; CHECK in (0, 1) |
| provider_loved | INTEGER | NULL allowed; CHECK in (0, 1) |
| current_action_id | TEXT | NULL allowed; references provider_actions(action_id) |
| created_utc, updated_utc | TEXT | NOT NULL |

An index supports loved-state and updated-time queries.

## Index and constraint inventory

- playback_observations: unique source/session/sequence/event identity and index on source_instance_id, session_id, captured_utc.
- plays: descending index on started_utc.
- provider_actions: unique idempotency_key; index on provider, account_id, state, due_utc; index on track_key and created_utc.
- accounts: unique provider/remote_username and index on provider/is_connected.
- loved_tracks: foreign keys to plays/provider_actions, boolean CHECK constraints, and index on is_loved/updated_utc.
- schema_migrations: primary key on migration identifier.

Foreign-key enforcement is explicitly enabled in the initial migration. Keep all new queries parameterized, keep timestamps UTC, and bound any JSON or free-form payload before persistence.

## Conceptual entities not present as standalone tables

The earlier architecture baseline described source_instances, play_state_transitions, provider_attempts, settings, exclusions, artwork_cache_entries, and analytics projections. **The current SQL migration chain does not create those tables.** Treat them as proposed domain concepts only unless a future migration makes them physical schema. Do not build a query, report, or migration on the assumption these tables exist today.

## Migration and verification workflow

1. Update the application/store code and create a new numbered migration for physical schema changes.
2. Keep the migration ID/resource registration order synchronized with SqliteDatabase.
3. Never alter an applied migration: checksum validation intentionally rejects content changes.
4. Add tests covering fresh initialization, existing-database upgrade, foreign keys, uniqueness/idempotency, and malformed or unsupported migration history.
5. Run the repository's Windows CI and manual backup/restore qualification when relevant.
6. Update backup/restore, retention, export, privacy, and data-flow docs if the change affects them.

Schema documentation does not replace inspecting the current migration files.
