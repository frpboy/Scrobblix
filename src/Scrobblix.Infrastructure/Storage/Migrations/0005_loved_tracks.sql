ALTER TABLE provider_actions ADD COLUMN track_key TEXT NULL;

CREATE INDEX IF NOT EXISTS ix_provider_actions_track_key
    ON provider_actions (track_key, created_utc);

CREATE TABLE IF NOT EXISTS loved_tracks (
    track_key TEXT PRIMARY KEY,
    source_play_id TEXT NOT NULL REFERENCES plays(play_id),
    artist TEXT NOT NULL,
    title TEXT NOT NULL,
    album TEXT NULL,
    is_loved INTEGER NOT NULL CHECK (is_loved IN (0, 1)),
    provider_loved INTEGER NULL CHECK (provider_loved IN (0, 1)),
    current_action_id TEXT NULL REFERENCES provider_actions(action_id),
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_loved_tracks_state_updated
    ON loved_tracks (is_loved, updated_utc DESC);
