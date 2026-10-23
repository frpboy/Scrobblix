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

## Now-playing expiry

Now-playing updates are ephemeral and expire five minutes after enqueue time. Before leasing the next action, the SQLite queue transaction cancels stale non-in-flight now-playing actions in Pending, RetryScheduled, AuthBlocked, RetryExhausted, or SubmissionOutcomeUnknown state with reason `provider.nowplaying.expired`. In-flight actions are left to their current worker, and actual Scrobble actions are never expired by this rule. This prevents a delayed retry from replacing a newer playback state after the original track has ended.

## User actions

Retry now schedules safely; cancel creates terminal local state; clearing queue requires confirmation. Users cannot mutate historical timestamps/payload in v1.
