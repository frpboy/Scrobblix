# Retention and Deletion — Policy vs Implementation

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Privacy requirements; full retention/deletion workflow is not established as shipped  
> **Last reviewed:** 2026-10-28

## Current persisted data

The migration-backed SQLite tables are `schema_migrations`, `playback_observations`, `plays`, `provider_actions`, `accounts`, and `loved_tracks`. Logs, non-secret UI preferences, and artwork cache are stored separately under the user profile. Last.fm session credentials and application API credentials are managed through Windows Credential Locker.

Do not describe source-transition, provider-attempt, analytics, exclusions, or retention-job tables as present unless a migration creates them. Refer to the [Database Schema](DATABASE_SCHEMA.md) for the exact physical schema.

## Privacy policy recommendations

The following are product policy targets that require explicit implementation and UI review; they should not be treated as proof of shipped cleanup workflows:

- Local listening history: retain until the user deletes it, with a clear disclosure and an accessible deletion control.
- Raw observations: choose a limited retention period or bounded quota before private/public release if full indefinite retention is not justified.
- Provider-action history: retain only enough local status to explain delivery and avoid duplicate reprocessing; define a capped/age-based cleanup policy.
- Logs: use a bounded rolling policy and redact track identities, secrets, authorization URLs and private paths.
- Artwork: bound per-item size and eventually document a disk/cache budget and eviction behavior.
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
