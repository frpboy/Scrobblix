PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS schema_migrations (
    migration_id TEXT PRIMARY KEY,
    checksum TEXT NOT NULL,
    applied_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS playback_observations (
    observation_id TEXT PRIMARY KEY,
    source_instance_id TEXT NOT NULL,
    session_id TEXT NULL,
    sequence_number INTEGER NOT NULL,
    event_id TEXT NULL,
    captured_utc TEXT NOT NULL,
    observed_utc TEXT NOT NULL,
    source_name TEXT NOT NULL,
    application_id TEXT NULL,
    artist TEXT NULL,
    title TEXT NULL,
    album TEXT NULL,
    duration_ms INTEGER NULL,
    position_ms INTEGER NULL,
    playback_state INTEGER NOT NULL,
    normalized_artist TEXT NULL,
    normalized_title TEXT NULL,
    UNIQUE (source_instance_id, session_id, sequence_number, event_id)
);

CREATE INDEX IF NOT EXISTS ix_playback_observations_source_time
    ON playback_observations (source_instance_id, session_id, captured_utc);

CREATE TABLE IF NOT EXISTS plays (
    play_id TEXT PRIMARY KEY,
    started_utc TEXT NOT NULL,
    ended_utc TEXT NULL,
    artist TEXT NULL,
    title TEXT NULL,
    album TEXT NULL,
    duration_ms INTEGER NULL,
    confirmed_played_ms INTEGER NOT NULL,
    eligibility_threshold_ms INTEGER NULL,
    eligible_utc TEXT NULL,
    local_status INTEGER NOT NULL,
    terminal_reason_code TEXT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_plays_started_utc ON plays (started_utc DESC);

CREATE TABLE IF NOT EXISTS provider_actions (
    action_id TEXT PRIMARY KEY,
    play_id TEXT NULL REFERENCES plays(play_id),
    account_id TEXT NOT NULL,
    provider TEXT NOT NULL,
    action_type INTEGER NOT NULL,
    state INTEGER NOT NULL,
    idempotency_key TEXT NOT NULL UNIQUE,
    attempt_count INTEGER NOT NULL DEFAULT 0,
    due_utc TEXT NOT NULL,
    lease_owner TEXT NULL,
    lease_expires_utc TEXT NULL,
    last_reason_code TEXT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    completed_utc TEXT NULL
);

CREATE INDEX IF NOT EXISTS ix_provider_actions_due
    ON provider_actions (provider, account_id, state, due_utc);
