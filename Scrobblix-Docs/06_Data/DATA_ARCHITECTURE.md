# Data Architecture — Current Persistence Model

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Implementation-backed data-path snapshot  
> **Last reviewed:** 2026-10-28

## Authoritative persistence

| Data | Current store | Notes |
|---|---|---|
| Playback observations | SQLite playback_observations | Local observed metadata/state/timestamps/source identity |
| Local plays | SQLite plays | Canonical local play timing, eligibility/status and metadata |
| Provider actions | SQLite provider_actions | Durable Last.fm action queue/status/leases/retry fields |
| Provider account state | SQLite accounts | Provider username, credential resource reference, connected/verification timestamps; not the secret itself |
| Loved-track state | SQLite loved_tracks | Local loved state and current provider action reference |
| Migration history | SQLite schema_migrations | Migration ID, checksum and applied UTC |
| Credentials | Windows Credential Locker abstraction | API key/shared secret and user session key; raw values must not be stored in SQLite |
| Preferences | Local user-profile settings files as implemented by desktop settings code | Do not assume a SQLite settings table exists |
| Logs | Local rolling/numbered files under user data | Operational events can be potentially personal; redact track metadata/secrets |
| Artwork | Local cache files | Rebuildable, bounded per item; excluded from backup archive |
| Statistics | Queries/read models derived from local state | No separate analytics/projection table is established in the physical schema |

See [the physical Database Schema](DATABASE_SCHEMA.md) for actual columns, constraints and indexes.

## Current local data paths

The desktop app creates the user's Scrobblix local data directory and stores database, logs, preferences and cached artwork in separate locations. Exact paths depend on the implemented local-app data helpers and current build; do not claim a packaged/unpackaged dual-path abstraction unless that code has been added.

## Data-flow invariants

- Track observation is distinct from canonical local play.
- Local eligibility is distinct from a provider action being queued.
- A provider action being queued or written to the local pipe is distinct from Last.fm acceptance.
- Provider actions use local idempotency keys and leases; a remote timeout can still produce an ambiguous outcome.
- A user session secret must not appear in SQLite, browser storage, logs, backups or diagnostics.
- Deleting a local record, when supported, does not delete a remote Last.fm record.
- Artwork failure must not invent track metadata or block local play tracking.

## Current storage limitations

The current schema does not contain separate source-instance, transition-history, provider-attempt, analytics-projection, retention-policy or settings tables. Product/architecture documents may refer to these as concepts, but they are not physically persisted unless a later numbered migration creates them.

There is not a complete configurable retention scheduler or full user-facing delete-data workflow established as shipped. Retention/deletion behavior must be documented from actual UI/source, not inferred from privacy policy goals.

## Statistics and queries

Current Statistics views derive local summaries from available local history. They should label the selected basis and avoid describing all detected history as provider-accepted. Keep queries parameterized and bounded; add appropriate indexes/migrations with measured evidence for any new expensive aggregation.

## Backup boundary

Current backup ZIP contents are exactly a versioned manifest.json and scrobblix.db. The database snapshot is checksummed, schema/integrity validated and staged before a future launch applies it. Credentials, preferences outside the DB and artwork cache are not silently included in the archive. See [Backup and Restore](BACKUP_RESTORE.md).

## Change discipline

For every physical schema change, add a new immutable migration, update the database schema/migration documents, and add upgrade/recovery assertions. For changes to logs, exports, backup, preferences or credentials, update the privacy/retention and support-bundle docs too.
