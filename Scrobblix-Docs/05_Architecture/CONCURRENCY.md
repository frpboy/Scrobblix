# Concurrency and Threading

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Model

Use .NET hosted background services and asynchronous channels/queues. Avoid a shared mutable “current track” object accessed by UI, GSMTC callbacks, extension transport, and sync workers.

## Single-writer principle

A serialized playback command processor owns mutation of active play sessions. Source callbacks enqueue immutable observations. This removes most timing races and makes event-order tests deterministic.

## Work partitions

- UI thread: view updates and commands only.
- Observation ingress: bounded channel, validation/coalescing.
- Playback processor: single logical writer.
- Persistence: async transactional operations; connection strategy documented for SQLite.
- Provider sync: bounded concurrency, normally one Last.fm batch in flight per account.
- Analytics: incremental background projection with lower priority.
- Artwork: bounded worker pool and cache budget.

## Backpressure

- Position observations are coalesced by source/media identity; metadata/state transitions are never dropped.
- Native host enforces message rate/size limits.
- UI update stream samples progress without losing state changes.
- Queue sync is provider-rate aware.

## Cancellation and shutdown

All long-running operations accept cancellation. Shutdown stops new ingestion, commits in-memory transitions that are safe to commit, releases queue leases or lets leases expire, closes transport, and avoids blocking on remote calls beyond a short deadline.

## Time

Use monotonic timestamps for elapsed played-time intervals and UTC wall clock for persisted event/provider timestamps. Wall-clock changes trigger diagnostics but cannot inflate played time.
