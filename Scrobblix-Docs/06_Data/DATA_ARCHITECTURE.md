# Data Architecture

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Data classes

| Class | Examples | Store | Sensitivity |
|---|---|---|---|
| Credentials | Last.fm session key, release secrets | Windows Credential Locker / CI secrets | Secret |
| Playback observations | source metadata, position, state, timestamps | SQLite, retention-controlled | Personal |
| Canonical history | track, artist, album, source, timing, outcome | SQLite | Personal |
| Provider actions | scrobble/love queue payload and attempts | SQLite | Personal |
| Settings | preferences, exclusions, retention | typed local settings/SQLite | Personal/configuration |
| Artwork cache | thumbnails from permitted sources | bounded file cache | Copyrighted/cache |
| Logs | event IDs, errors, redacted context | rotating local files | Potentially personal |
| Analytics read models | aggregates derived from history | SQLite | Personal/derived |

## Authoritative sources

- Playback/history and queue state: SQLite.
- Provider session secret: Credential Locker.
- Runtime current state: reconstructed from SQLite plus current source snapshots.
- Analytics: derived and rebuildable from canonical history.
- Artwork: non-authoritative cache.

## Storage locations

Use Windows known folders and package APIs rather than hard-coded paths. The UI exposes actual paths. Packaged/unpackaged layouts must be abstracted so backup, support, and uninstall behavior remain consistent.

## Invariants

- A queue item references exactly one canonical play or explicit provider action.
- Eligibility and queue insertion commit in one transaction.
- A canonical play has zero or one scrobble queue action per provider/account.
- Provider attempts are append-only audit records.
- Raw secret material never appears in SQLite.
- Deleting a play cascades/reconciles derived local analytics but does not imply provider deletion.

## SQLite configuration

Enable foreign keys. Evaluate WAL mode on supported package/storage paths. Use busy timeouts and short transactions. Schema migrations run before background services. Read-heavy views use indexes and projections; UI never performs unbounded `SELECT *` queries.
