# Retention and Deletion — Policy vs Implementation

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Privacy requirements; full retention/deletion workflow is not established as shipped  
> **Last reviewed:** 2026-11-07

## Current persisted data

The migration-backed SQLite tables are `schema_migrations`, `playback_observations`, `plays`, `provider_actions`, `accounts`, and `loved_tracks`. Logs, non-secret UI preferences, and artwork cache are stored separately under the user profile. Last.fm session credentials and application API credentials are managed through Windows Credential Locker.

Do not describe source-transition, provider-attempt, analytics, exclusions, or retention-job tables as present unless a migration creates them. Refer to the [Database Schema](DATABASE_SCHEMA.md) for the exact physical schema.

## Approved shipped policy: raw playback observations (2026-11-06)

The repository owner approved a 30-day retention period for raw `playback_observations` diagnostics on 2026-11-06 (issue [#98](https://github.com/frpboy/Scrobblix/issues/98)). Shipped behavior:

- Raw observations older than 30 days are deleted by a background task at app start and every 24 hours while the app runs. No cleanup runs while the app is closed; expired rows wait until the next launch.
- Cleanup deletes in bounded batches of 500 rows inside individual transactions, honors cancellation between batches, and retries on the next cycle after a failure; only deleted row counts are logged, never listening metadata.
- Canonical `plays` listening history, `provider_actions` (including pending or ambiguous submissions), `accounts` and `loved_tracks` are never touched by this cleanup.
- Migration `0006_observation_retention` adds the `ix_playback_observations_captured_utc` index backing the expiry query.

## Approved shipped policy: artwork cache (2026-11-07)

The repository owner approved a 30-day retention period for cached artwork on 2026-11-07 (issue [#122](https://github.com/frpboy/Scrobblix/issues/122)). Shipped behavior:

- Cached artwork files whose last write is older than 30 days are deleted by a background task at app start and every 24 hours while the app runs. As with raw observation cleanup, no cleanup runs while the app is closed.
- Cached artwork still referenced by retained listening history (`plays.artwork_path`) is preserved, so history artwork remains available; listening history itself is kept and is not covered by this policy.
- Orphaned temporary files from interrupted artwork writes are removed after a 1-day grace period.
- Cleanup deletes in bounded batches, honors cancellation between batches, skips locked or inaccessible files for the next cycle, and logs only file counts, never track metadata.
- Only image files (`.png`, `.jpg`, `.jpeg`, `.webp`, `.gif`, `.bmp`) and `.tmp` files directly inside the artwork cache directory are eligible for removal; other files, subdirectories, the raw history/provider queue, and the rest of the profile directory are never touched.
- The in-memory GSMTC artwork key map is bounded independently at 2,048 entries with FIFO eviction; evicted keys are revalidated from disk on next use.

An aggregate byte budget for the cache remains an open policy target; the owner selected age-based eviction.

Other data classes below remain policy targets until individually implemented and reviewed.

## Privacy policy recommendations

The following are product policy targets that require explicit implementation and UI review; they should not be treated as proof of shipped cleanup workflows:

- Local listening history: retain until the user deletes it, with a clear disclosure and an accessible deletion control.
- Raw observations: choose a limited retention period or bounded quota before private/public release if full indefinite retention is not justified.
- Provider-action history: retain only enough local status to explain delivery and avoid duplicate reprocessing; define a capped/age-based cleanup policy.
- Logs: use a bounded rolling policy and redact track identities, secrets, authorization URLs and private paths.
- Artwork: age-based cache eviction is shipped (see the approved policy above); an aggregate byte budget remains an open policy target.
- Support bundle: user-initiated diagnostics-only export; no automatic upload. Credentials, listening database/history, artwork, free-form log messages, and raw crash dumps are excluded by design.

## Current disconnect behavior

Disconnecting Last.fm removes the locally stored session credential and disconnects the provider account, which blocks future provider queue delivery for that account. Local listening history is not automatically deleted. Revoking the authorization at Last.fm is a separate provider-side action.

Do not describe automatic deletion of all pending actions, local plays, cached artwork, or logs as current behavior unless the corresponding implementation and regression tests are present.

## Deletion requirements before release

Before beta, define and test:
- delete one local play and its related local observation/action behavior;
- delete all local history and the implications for queued or ambiguous submissions;
- disconnect/revoke an account and safely handle pending actions;
- clean up preferences/cache/logs on uninstall or explain exactly what is retained;
- retention expiry behavior, storage limits and recoverability;
- backup/restore interaction with deletion and privacy settings.

The current implementation should never claim secure erasure beyond the operating system and filesystem guarantees. If deletion is implemented, stop relevant writers, make state changes transactionally consistent, and keep provider-side scrobbles distinct: deleting local history does not delete an already accepted Last.fm record.

## Verification

Do not mark a retention/deletion scenario PASS based on policy text. Record exact candidate SHA and real behavior in the test matrix; leave incomplete scenarios as NOT RUN.

## Backup, restore and diagnostic retention

Existing backup archives are immutable snapshots and can retain raw observations older than the active database window. Cleanup does not rewrite or delete archive files. A restored database follows the same startup cleanup policy, so expired raw observations can be pruned after restore. Keep a separate archived backup when diagnostics must be retained for investigation. There is no retention opt-out or diagnostic-history selector in this build. Canonical listening history and queued/ambiguous provider actions remain unchanged. This is logical SQLite deletion, not secure erasure; freed pages may remain in the database file and can be reused. Cleanup does not run VACUUM or promise immediate on-disk file shrinkage.
