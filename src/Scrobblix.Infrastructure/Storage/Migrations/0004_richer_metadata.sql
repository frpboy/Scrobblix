ALTER TABLE plays ADD COLUMN album_artist TEXT NULL;
ALTER TABLE plays ADD COLUMN track_number INTEGER NULL;
ALTER TABLE plays ADD COLUMN release_year INTEGER NULL;
ALTER TABLE plays ADD COLUMN recording_mbid TEXT NULL;
ALTER TABLE plays ADD COLUMN release_mbid TEXT NULL;
ALTER TABLE plays ADD COLUMN artwork_source TEXT NULL;

ALTER TABLE playback_observations ADD COLUMN artwork_path TEXT NULL;
ALTER TABLE playback_observations ADD COLUMN artwork_source TEXT NULL;
ALTER TABLE playback_observations ADD COLUMN album_artist TEXT NULL;
ALTER TABLE playback_observations ADD COLUMN track_number INTEGER NULL;
ALTER TABLE playback_observations ADD COLUMN release_year INTEGER NULL;
ALTER TABLE playback_observations ADD COLUMN recording_mbid TEXT NULL;
ALTER TABLE playback_observations ADD COLUMN release_mbid TEXT NULL;
