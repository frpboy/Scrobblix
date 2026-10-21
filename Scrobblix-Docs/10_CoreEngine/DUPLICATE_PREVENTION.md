# Duplicate Prevention

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Duplicate layers

1. Raw observation duplicate: same source/session/sequence/event.
2. Canonical duplicate: multiple sources represent one play.
3. Queue duplicate: same provider/account/play/action.
4. Historical/provider ambiguity: retry after uncertain response.

## Keys

- Source event key: source instance + session + sequence/event identity.
- Canonical correlation key: normalized media identity + timing bucket + session evidence; not sufficient alone, used with rules.
- Provider action idempotency key: provider + account + action type + canonical play ID.

Database unique constraints enforce queue-level uniqueness.

## Repeats

Consecutive genuine plays of the same track must scrobble separately. A restart/position reset near zero after a credible end/track-change/replay action creates a new canonical play. Time-window deduplication alone is prohibited.

## Ambiguous HTTP outcome

If timeout occurs after request transmission, retry may duplicate remotely. Record ambiguity, use conservative backoff, and optionally reconcile recent provider history within rate/terms limits. Never silently mark accepted without evidence.
