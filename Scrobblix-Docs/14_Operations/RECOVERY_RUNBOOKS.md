# Recovery Runbooks — Current Preview

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Operational guidance; live recovery qualification remains pending  
> **Last reviewed:** 2026-10-28

## Last.fm invalid session

1. Open Settings and review the Last.fm connection state.
2. Reconnect through Last.fm browser authorization; do not enter a Last.fm password into Scrobblix.
3. Verify the account identity shown after completing the token/session exchange.
4. Inspect queued action states after reconnection. Preserve pending scrobbles and do not silently reassign old-account actions to a new account.
5. Validate remote delivery with a dedicated test profile and the matching local action ID.

Never place API keys, shared secrets, session keys or authorization tokens in an issue or support archive.

## Provider outage or rate limiting

Keep local history and durable provider actions. The current queue schedules bounded exponential retry delays or honors a usable `Retry-After` hint (capped at 24 hours). After eight automatic transient attempts, an action can enter `RetryExhausted`. An invalid session becomes `AuthBlocked`; ambiguous submission outcomes remain explicit.

The current code does not implement a general circuit breaker or guaranteed provider batch flush. Do not claim that remote actions will be accepted at a specific time. Inspect reason/state and allow scheduled recovery; retry ambiguous scrobble submissions only after considering duplicate risk.

## Native host missing or browser connection failing

1. Use the extension popup's **Test connection** diagnostic to distinguish host registration, startup, identity and protocol issues.
2. Confirm Scrobblix and browser extension are installed for the same Windows user.
3. Copy the actual extension ID from the affected browser (Chrome and Edge may differ).
4. Run `scripts/setup-browser-companion.ps1` with the correct browser and exact ID to build/register/repair the host as appropriate.
5. Verify the per-user native-host manifest/executable path, restart the affected browser, then rerun the diagnostic.

Do not elevate the installer or remove arbitrary registry entries as a default remedy. The companion is optional when GSMTC metadata is sufficient. A host-level `accepted.v1` means a playback observation frame was forwarded to the desktop pipe, not that a scrobble succeeded.

## Database integrity or migration failure

1. Stop normal processing and record the exact app build and sanitized error.
2. Preserve the active database and any reported rollback/safety-snapshot files before attempting recovery.
3. Use a known-good Scrobblix backup and the built-in staged restore path; validate ZIP entries, manifest/checksums, schema and integrity.
4. Keep the original and rollback artifacts if replacement fails; do not delete them until recovery is complete.
5. Never edit an already-applied SQL migration; the app verifies migration checksums and rejects incompatible history.
6. Do not reset the database unless the user explicitly accepts local-history loss and any backup/export has been considered.

## Disk full or storage write failure

Preserve the database and queued actions. Close the app safely, free user-controlled disk space without deleting the database or credential store, then restart and inspect the storage status. Avoid repeated retries while the disk is full. Cached artwork may be replaceable, but confirm the target path before removing anything.

## Unexpected application exit

Record the exact candidate SHA/app version, timestamp/time zone, Windows Application event and exit code. Capture the last relevant local log lines after redaction; retain a crash dump only if Windows produced one and protect it as potentially sensitive. The previously observed pipe retry storm and build-output lock did not alone prove the root cause of every unexpected exit.

Reproduce with the Browser Companion enabled/disabled, a clean app start, normal exit, sleep/wake and restart. Do not mark a crash fixed because CI builds; the corresponding manual test must pass on Windows.

## Bad release or migration

Current downloads are unsigned preview ZIPs, not signed production installers. Do not describe them as signed or advise users to bypass OS warnings. If a preview fails, stop recommending that exact tag, preserve local data and provide a corrected, newly tagged/validated preview. Signed release, automatic update and binary rollback workflows remain future gates.

## Diagnostics and privacy

A support bundle is manually exported and not automatically uploaded. It excludes the listening database, artwork, credentials, full URLs, raw log messages, browser tab/site identity and raw crash dumps by design. Inspect the archive and share only after reviewing it. The Windows Save/export/privacy/accessibility qualification is still pending.
