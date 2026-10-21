# Data Management

## Export history

Users can export selected history as CSV or versioned JSON. Export includes the chosen date range and clearly labeled local/provider statuses. Secrets and internal authentication material are never exported.

## Backup

A full backup includes the database, non-secret settings, schema metadata, and compatibility information. Artwork and rebuildable caches may be optional. Provider credentials require reconnection unless a separately designed encrypted secret-export feature exists.

## Restore

Restore performs these steps before replacing active data:

1. Verify archive structure and size limits.
2. Validate backup and schema versions.
3. Check database integrity.
4. Create a rollback copy of current data.
5. Run migrations in an isolated transaction/copy.
6. Present a summary of imported records and skipped items.
7. Resume background services only after validation succeeds.

## Delete local data

A full local-data deletion should offer separate choices for history, pending queue, settings, logs, artwork, browser integration, and credentials. The operation must explain that deletion does not remove scrobbles already accepted by Last.fm.

## Portability

JSON is the canonical machine-readable export. CSV is optimized for analysis and human inspection but may not preserve every state transition or nested diagnostic field.
