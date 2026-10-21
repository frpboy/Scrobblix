# Queue

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Purpose

Expose every remote action that has not reached a terminal successful/ignored state and provide safe recovery controls.

## Tabs

- Pending.
- Retrying.
- Needs attention.
- Completed recent (optional short retention).

## Queue row

Track/action, original listen time, queued time, state, attempt count, next attempt, reason, provider, and actions.

## States and language

| Internal state | User label |
|---|---|
| pending | Ready to send |
| leased/in_flight | Sending |
| retry_scheduled | Retrying at … |
| auth_blocked | Sign in required |
| rate_limited | Waiting for Last.fm |
| permanent_failure | Could not submit |
| cancelled | Cancelled locally |
| accepted | Scrobbled |
| provider_ignored | Last.fm ignored |

## Actions

- Retry now.
- Retry all due.
- Reauthenticate.
- Cancel/delete queued action.
- Copy sanitized technical details.
- Open linked history record.
- Pause/resume synchronization.

## Safety

Retry must not change original start timestamp. Editing payload is not supported in v1; correction requires creating an explicitly audited replacement/manual workflow in a later feature.

## Queue detail

Show immutable original payload (redacted), normalized payload, duplicate key, attempts, timestamps, HTTP/provider classifications, ignored message, next retry rule, and correlation ID.
