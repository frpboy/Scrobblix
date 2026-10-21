# Reason Codes

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Playback/local

- `playback.waiting_for_eligibility`
- `playback.track_too_short`
- `playback.duration_unknown`
- `playback.insufficient_confirmed_time`
- `playback.source_lost_before_eligibility`
- `playback.tracking_paused`
- `playback.non_music`
- `playback.advertisement`
- `metadata.missing_artist`
- `metadata.missing_title`
- `metadata.invalid_duration`
- `source.excluded_by_user`
- `duplicate.merged_sources`
- `duplicate.provider_action_exists`

## Provider

- `provider.accepted`
- `provider.ignored.artist`
- `provider.ignored.track`
- `provider.ignored.timestamp_old`
- `provider.ignored.timestamp_new`
- `provider.ignored.daily_limit`
- `provider.auth.required`
- `provider.auth.invalid_session`
- `provider.invalid_api_key`
- `provider.invalid_signature`
- `provider.suspended_key`
- `provider.rate_limited`
- `provider.temporary_unavailable`
- `provider.response_invalid`

## Platform/storage

- `extension.not_installed`
- `extension.native_host_missing`
- `extension.protocol_version_mismatch`
- `extension.permission_required`
- `gsmtc.unavailable`
- `storage.write_failed`
- `storage.database_integrity_failed`
- `system.clock_invalid`

Each code maps to severity, terminal/retry behavior, localized title/body, evidence fields, and recommended action IDs. Codes are never repurposed with different meaning.
