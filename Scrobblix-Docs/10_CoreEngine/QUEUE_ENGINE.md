# Provider Action Queue — Implementation Snapshot

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Source-backed queue behavior; real-provider qualification pending  
> **Last reviewed:** 2026-10-28

## Persistent action model

`provider_actions` in SQLite is the durable queue for Last.fm scrobble, Now Playing, Love and Unlove work. Each row records provider/account, action type/state, unique local idempotency key, attempt count, due time, optional lease owner/expiry, last reason code, and creation/update/completion timestamps; `track_key` was added by migration `0005_loved_tracks`. The associated `plays` row carries artist/title/album, original start time and duration used to construct the provider request.

The queue is scoped to provider and account. Do not describe a provider-independent batch transport or multiple-track Last.fm batch API as implemented; the current provider submits one action at a time.

## Claim and lease lifecycle

The current SQLite store claims one due `Pending` or `RetryScheduled` item inside a transaction, orders by due time then creation time, changes it to `InFlight`, increments attempt count and records the lease owner/expiry. Startup recovery converts expired in-flight leases into immediately due retry work with the `provider.lease_expired` reason.

Queue state is local durable state. An `Accepted` action indicates the provider response was parsed as accepted; it must not be set merely because an HTTP request was started or sent.

## Retry and terminal states

- Retry delay follows a provider `Retry-After` hint when valid, capped at 24 hours.
- Otherwise the processor uses bounded exponential delays: 1, 2, 4, 8, 16, 32, then 60 minutes (the calculation is capped at 60 minutes).
- After the automatic transient-attempt ceiling of eight is reached, a further transient outcome becomes `RetryExhausted`.
- Invalid session becomes `AuthBlocked`.
- Explicit provider-ignored reasons become `ProviderIgnored`.
- Non-transient failures become `PermanentFailure`.
- Timeouts/network failures or malformed/ambiguous scrobble responses may become `SubmissionOutcomeUnknown`.

The current code does **not** implement full-jitter retry delays or a general circuit breaker. Do not promise those behaviors based on older aspirational queue designs.

## Ambiguous scrobble outcomes

A timed-out POST might have been accepted by Last.fm even though Scrobblix received no usable response. `SubmissionOutcomeUnknown` is not synonymous with rejected or safe to retry automatically. The UI provides an explicit path to review the outcome; a user-confirmed accepted result can be recorded, while retrying an ambiguous action must be an informed decision with duplicate risk understood.

## Now Playing behavior

Now Playing is ephemeral rather than a durable historical scrobble. The SQLite claim transaction cancels stale, non-in-flight Now Playing actions older than five minutes in the specified nonterminal states, with `provider.nowplaying.expired`. Actual scrobble actions are not expired by this rule. Do not replay stale Now Playing state after a newer track is active.

## User commands

The implementation supports retry-now for eligible queue states, a specific action path for retrying exhausted Last.fm scrobbles, cancellation of eligible pending/retry states, and explicit resolution of an ambiguous outcome as accepted. These paths have distinct predicates; keep their semantics separate and regression-test state transitions to prevent accidental duplicate deliveries.

## Verification boundary

Automated tests cover selected lifecycle, state, lease and recovery rules. Real network/provider acceptance, multi-day listening correctness, account reconnect behavior and long-duration reliability require a real Windows/Last.fm test profile and remain manual release gates.
