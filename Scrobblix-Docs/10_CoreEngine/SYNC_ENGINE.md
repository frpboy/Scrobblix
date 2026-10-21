# Provider Synchronization Engine

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Scheduler inputs

Network state, provider health/circuit, account auth, queue due times, app lifecycle, user pause setting, and manual retry.

## Batch process

1. Select compatible scrobble actions for same provider/account.
2. Lease up to provider limit (50 for Last.fm).
3. Build signed payload with original timestamps.
4. Submit with timeout/cancellation policy.
5. Parse global and per-item responses.
6. Commit each outcome and release/extend leases transactionally.
7. Emit local status/notification events.

## Error classification

Transport timeout/DNS/TLS, HTTP, Last.fm `lfm` error, per-scrobble ignored message, invalid session, rate limit, suspended key, malformed response. Classification determines retry/block/permanent and health transition.

## Circuit breaker

After repeated systemic provider failures, stop immediate attempts, schedule health probe/backoff, and keep queueing locally. Manual retry should not create a tight loop.

## Concurrency

One scrobble batch per account/provider; independent providers could run separately later. Database lease/version prevents duplicate worker completion.
