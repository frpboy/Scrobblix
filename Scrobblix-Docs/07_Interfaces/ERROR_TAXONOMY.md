# Error and Reason Taxonomy — Current Codes

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Source-backed reason-code reference  
> **Last reviewed:** 2026-10-28

## Authority

The stable code constants currently live in `src/Scrobblix.Domain/Diagnostics/ReasonCodes.cs`. Use those exact names in persistence, logs, tests and user-facing status mapping. Do not mint a new spelling or infer that every future planned subsystem already has an implemented code family.

## Current reason-code families

| Family | Example/current constants | Purpose |
|---|---|---|
| Playback | `playback.waiting_for_eligibility`, `playback.track_too_short`, `playback.duration_unknown`, `playback.insufficient_confirmed_time`, `playback.source_lost_before_eligibility`, `playback.tracking_paused`, `playback.non_music`, `playback.advertisement` | Local tracking/eligibility decisions |
| Metadata | `metadata.missing_artist`, `metadata.missing_title`, `metadata.invalid_duration` | Missing or invalid identifying metadata |
| Source | `source.excluded_by_user`, `source.lower_priority_observation` | Exclusion and source arbitration |
| Duplicate | `duplicate.merged_sources`, `duplicate.provider_action_exists` | Dedupe and action idempotency |
| Provider | `provider.lease_expired`, `provider.accepted`, `provider.auth.required`, `provider.auth.invalid_session`, `provider.rate_limited`, `provider.temporary_unavailable`, `provider.submission_outcome_unknown`, `provider.retry_exhausted`, `provider.response_invalid`, `provider.nowplaying.expired` plus the provider-specific `provider.ignored.*` codes | Queue, provider response and retry state |
| Extension | `extension.not_installed`, `extension.native_host_missing`, `extension.protocol_version_mismatch`, `extension.permission_required` | Browser companion readiness and permission state |
| GSMTC | `gsmtc.unavailable` | Windows media source unavailable |
| Storage | `storage.write_failed`, `storage.database_integrity_failed` | Local persistence/integrity failures |
| System | `system.clock_invalid` | Wall-clock sanity issue |

The native host and extension also use bounded protocol error codes such as `extension.invalid_frame`, `extension.invalid_envelope`, `extension.untrusted_sender`, `extension.unsupported_message`, `extension.host_not_connected`, and popup diagnostic strings. Those are protocol-level responses, not necessarily persisted Domain `ReasonCodes` constants.

## Error data contract

Where exposed by the caller, preserve:
- stable reason code;
- action/play correlation ID when safe;
- retryable/permanent/auth-blocked/ignored/unknown outcome classification;
- whether user intervention is required;
- bounded provider/protocol code needed to recover.

The current implementation does not create a single universal Error object with every proposed field in all paths. Do not make callers depend on such a shape until it exists in code.

## UX and logging

Translate stable codes to useful user-facing language; do not expose raw exceptions as the only explanation. Log only minimized identifiers and redacted diagnostics. Never include API key/shared secret/session key, signed API requests, authorization token, track/artist/album fields or arbitrary full URLs in logs/support bundles unless an explicit reviewed contract permits them.

## Change checklist

When adding or renaming a reason code:
1. Add/update the constant.
2. Update state mapping and UI explanation.
3. Add positive and negative regression tests.
4. Update this list and any schema/release/support docs.
5. Confirm existing stored reason codes remain understandable across migration and application upgrades.
