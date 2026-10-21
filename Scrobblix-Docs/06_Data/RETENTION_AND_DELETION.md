# Retention and Deletion

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Defaults

- Canonical local history: unlimited by default, clearly disclosed.
- Raw observations/state transitions: retain 30–90 days by default, configurable within safe bounds.
- Provider attempts: 90 days or a capped count.
- Completed queue items: minimal audit retention, then represented through play/provider status.
- Logs: rolling size/time policy; verbose logs expire quickly.
- Artwork: LRU/expiry under a user-visible size budget.

## Deletion semantics

### Delete one local play

Removes the local play and dependent local observations/transitions/analytics. If a provider action is still pending, ask whether to cancel it. An already accepted Last.fm scrobble remains remote.

### Delete all history

Requires explicit scope confirmation, stops writers, creates optional safety backup, deletes canonical history and derived models, clears relevant caches, and records only a minimal non-personal audit that deletion occurred if needed.

### Disconnect Last.fm

Deletes the session credential and blocks/cancels provider actions according to user choice. Local history is retained unless separately deleted.

### Uninstall

Document whether app data is removed by package uninstall and provide an in-app “delete all local data” action before uninstall. Do not claim secure erasure beyond OS/filesystem guarantees.

## Retention job

Runs opportunistically with bounded batches, never during migration/backup, and reports failures without blocking core tracking. Deletion cascades are transactionally consistent.
