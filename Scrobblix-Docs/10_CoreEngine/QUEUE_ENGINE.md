# Queue Engine

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Action types

Scrobble, love, unlove, and optionally ephemeral now-playing audit. Each action has provider/account, payload, state, due time, attempts, lease, and idempotency key.

## Enqueue

Eligibility transaction updates play and inserts scrobble action. Unique constraint makes repeated eligibility idempotent. Love/unlove commands may collapse superseded pending opposite actions if audit rules preserve user intent.

## Leasing

Worker atomically selects due items, assigns lease owner/expiry, and transitions to in-flight. Expired leases recover on startup/schedule. One account/provider batch at a time by default.

## Retry schedule

Exponential backoff with full jitter, bounded maximum, provider `Retry-After`, network restoration trigger, and circuit breaker for systemic failures. Attempt count does not erase actions automatically; permanent/retry-exhausted policy is explicit and user-visible.

## Ordering

Scrobbles preserve original timestamps and can batch by due order. Love/unlove ordering for same track/account must preserve latest intent. Now-playing is never replayed after staleness threshold.

## User actions

Retry now schedules safely; cancel creates terminal local state; clearing queue requires confirmation. Users cannot mutate historical timestamps/payload in v1.
