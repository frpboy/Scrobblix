# Fault Tolerance and Recovery

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Failure isolation

A failing source adapter is quarantined/restarted without stopping queue sync or UI. A provider outage does not stop detection/history. Analytics failure does not block scrobbling. Artwork failure never fails a play.

## Retry classification

- Immediate no-retry: validation, unsupported action, permanent provider rejection.
- User-action blocked: authentication, permission, missing host registration.
- Scheduled retry: network, timeout, provider temporary unavailable, rate limit.
- Reconcile-before-retry: ambiguous timeout after sending a non-idempotent scrobble. Because Last.fm lacks client idempotency, local duplicate protection and cautious retry policy are required; provider recent-history reconciliation may be considered but must avoid excessive read calls.

## Queue leases

An action lease includes owner, acquired time, and expiry. Crash leaves the item recoverable after expiry. Completion uses optimistic concurrency/version check so two workers cannot finalize the same item.

## Database protection

- WAL mode where validated.
- Foreign keys enabled.
- Transactions for canonical play + eligibility + queue insertion.
- Startup quick-check; scheduled deeper integrity check.
- Pre-migration backup for risky migrations.
- Corruption recovery attempts read-only export before reset.

## Source reconnect

On adapter reconnect, ingest a full current snapshot with sequence/session identity. The engine determines whether it is continuation, handoff, or new play. Do not simply replay missed high-frequency observations.

## Degraded modes

- Local-only.
- GSMTC-only.
- Extension-only for supported tabs if GSMTC unavailable.
- History read-only if database write health fails.
- Queue paused if system clock is implausible.

Every degraded mode is visible and has an exit/repair path.
