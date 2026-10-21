# Acceptance Criteria Catalog

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## AC-ELIG-001 — Standard track

**Given** a 300-second track with valid artist/title,  
**when** confirmed playing time reaches 150 seconds,  
**then** the canonical play becomes eligible once, a queue item is persisted, and subsequent observations cannot create a second item for the same play.

## AC-ELIG-002 — Long track

Given a 900-second track, it becomes eligible at 240 seconds, not 450 seconds.

## AC-ELIG-003 — Short track

Given a track duration of 30 seconds or less, it never becomes eligible and ends with `track_too_short`.

## AC-PLAY-001 — Pause handling

Given a playing track paused for 120 seconds, paused wall time does not increase confirmed played time or eligibility progress.

## AC-PLAY-002 — Sleep/resume

Given the machine sleeps during playback, the sleep interval is not counted unless the source provides reliable evidence that playback continued and the policy explicitly supports it; default behavior is conservative.

## AC-ARB-001 — Browser merge

Given matching extension and GSMTC observations within the correlation window, one canonical play exists, extension metadata may fill missing fields, and only one submission key is created.

## AC-QUEUE-001 — Crash durability

Given eligibility is reached and the process is terminated after commit but before HTTP completion, restart finds exactly one due queue item with the original timestamp.

## AC-QUEUE-002 — Partial batch

Given a batch response accepts some items and ignores/fails others, each queue item receives its own mapped outcome; accepted items are not retried.

## AC-AUTH-001 — Expired session

Given provider error 9/invalid session, due provider actions move to authentication-blocked, the user is prompted once, and reauthentication unblocks them.

## AC-UX-001 — Explain failure

Given any terminal non-success state, history detail shows a user-facing explanation, stable reason code, source, relevant timing/metadata, and at least one applicable action or an explicit “No action required.”

## AC-PRIV-001 — Extension permissions

Given a fresh extension install, no optional website access is granted until the user enables a supported site or chooses a documented broader mode.

## AC-SEC-001 — Secret redaction

Given logs, crash data, exports, and support bundles, seeded API/session secret canary values never appear in output.

## AC-ACC-001 — Keyboard

Given only keyboard input, a user can connect account, navigate Home/History/Queue/Diagnostics/Settings, open details, retry an item, and exit.
