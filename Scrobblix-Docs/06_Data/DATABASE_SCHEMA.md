# Database Schema

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Conventions

SQLite schema uses `snake_case`, UTC timestamps, explicit foreign keys, and migration-managed indexes. UUID/ULID text keys are preferred for cross-process/event correlation. The exact storage representation is finalized in migration 0001 and remains stable.

## Core tables

### `accounts`

| Column | Type | Notes |
|---|---|---|
| account_id | TEXT PK | Local account identifier |
| provider | TEXT | `lastfm` initially |
| remote_username | TEXT | Non-secret profile identity |
| is_connected | INTEGER | Derived cached state, not proof of credential validity |
| connected_utc | TEXT NULL | |
| last_verified_utc | TEXT NULL | |
| created_utc | TEXT | |
| updated_utc | TEXT | |

Credential reference names may be stored; secret values may not.

### `source_instances`

`source_instance_id`, source kind, application/browser/site identifiers, display name, adapter version, first/last seen, enabled state, health, metadata JSON limited to non-secret compatibility fields.

### `playback_observations`

`observation_id`, `source_instance_id`, source session/tab key, sequence, captured UTC, monotonic tick snapshot, raw/normalized artist/title/album, duration, position, playback state, artwork reference, chosen-by-user hint, metadata completeness, payload version, canonical play link. The source-event identity is `(source_instance_id, session_id, sequence_number, event_id)`. Because SQLite UNIQUE constraints treat `NULL` values as distinct, the storage boundary persists absent optional `session_id` and `event_id` identity components as empty strings so repeated source events remain idempotent. Source instance IDs must be unique for each collector lifetime when sequence numbers restart. Retention may purge these before canonical history.

### `plays`

| Column | Purpose |
|---|---|
| play_id | Canonical identity |
| account_id | Optional provider account context |
| started_utc | Original detected start estimate |
| ended_utc | Terminal time |
| artist/title/album/album_artist | Canonical payload fields |
| duration_seconds | Best accepted duration |
| confirmed_played_ms | Accumulated playing intervals |
| eligibility_threshold_ms | Stored decision evidence |
| eligible_utc | First eligibility time |
| local_status | active/ended/abandoned/ignored/duplicate |
| terminal_reason_code | Stable reason |
| winning_source_instance_id | Arbitration result |
| duplicate_of_play_id | If merged/duplicate |
| created_utc/updated_utc | Audit |

### `play_state_transitions`

Append-only: transition ID, play ID, from/to state, event type, reason code, event UTC, monotonic marker/delta, source observation, correlation ID, sanitized detail JSON.

### `provider_actions`

| Column | Purpose |
|---|---|
| action_id | Queue identity |
| play_id | Nullable for non-play actions |
| account_id | Required |
| provider | Last.fm |
| action_type | scrobble/now_playing/love/unlove |
| state | pending/in_flight/retry/auth_blocked/etc. |
| payload_json | Validated non-secret payload |
| idempotency_key | Unique local duplicate key |
| attempt_count | |
| due_utc | Retry schedule |
| lease_owner/lease_expires_utc | Concurrency |
| last_reason_code | |
| created_utc/updated_utc/completed_utc | |

Now-playing may be persisted as an audit action but must expire and never be retried as stale.

### `provider_attempts`

Attempt ID, action ID, started/completed UTC, request classification (not full signature), HTTP status, provider error/ignored code, result classification, retry-after, latency, correlation ID, redacted response summary.

### `settings`

Typed key/value/version rows only for settings not better handled by platform settings. Validation occurs in application layer. Sensitive values prohibited.

### `exclusions`

Scope (`application`, `site`, `source_instance`), match key, enabled, optional reason, created/updated.

### `artwork_cache_entries`

Content hash, local relative path, source URI hash (not necessarily raw URL), media type, dimensions, byte size, last access, expiry, attribution fields.

### Analytics tables

`daily_summary`, `entity_play_counts`, `listening_sessions`, and migration/version metadata are rebuildable projections.

## Required indexes

- plays by started time descending.
- plays by normalized artist/title and started time.
- plays by local/provider status.
- observations by source/session/captured time.
- provider actions by state/due/account/provider.
- unique provider action idempotency key.
- transitions by play/time.
- attempts by action/time.

## Schema integrity

Use check constraints where SQLite supports them reliably, application validation, foreign keys, unique indexes, and migration tests. Arbitrary JSON fields are bounded and schema-versioned; do not use JSON as a substitute for normalized query-critical fields.
