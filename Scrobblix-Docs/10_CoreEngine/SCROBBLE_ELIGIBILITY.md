# Scrobble Eligibility — Implementation Contract

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Domain implementation and policy snapshot  
> **Last reviewed:** 2026-10-28

## Implemented decision rule

The current implementation is `src/Scrobblix.Domain/Eligibility/ScrobbleEligibility.cs`. It evaluates a candidate in this order:

1. Artist must be non-empty.
2. Track title must be non-empty.
3. Duration must be known.
4. Duration must be positive and **strictly greater than 30,000 ms**.
5. User exclusion, duplicate/canonical-source merge, existing provider action, non-music, and advertisement checks must all be false.
6. Confirmed played time must reach the eligible threshold.

The threshold in integer milliseconds is:

    min(durationMilliseconds / 2, 240000)

So a track of more than 30 seconds requires half its duration, capped at four minutes. Integer division rounds a half-millisecond down for odd millisecond values. The decision returns the calculated threshold while still ineligible for insufficient confirmed time, allowing the UI/tracker to show progress.

## Unknown or invalid duration

Unknown duration is ineligible with `playback.duration_unknown`; zero/negative duration is `metadata.invalid_duration`; duration at or below 30,000 ms is `playback.track_too_short`. The policy deliberately does not infer eligibility from four minutes alone when the duration is unknown.

## Confirmed played time

The eligibility function receives confirmed played milliseconds; it does not infer listened time directly from the current media position. The tracker uses monotonic elapsed intervals in playing state and source arbitration/observation handling to avoid counting paused time and unseen source gaps as listening. Seeking and replay detection need to remain regression-tested separately from the eligibility formula.

When the candidate first crosses the threshold, the local play store persists eligibility and the durable provider-action path can enqueue a scrobble. Repeated observations must not create another action for the same qualifying play. A deliberate same-track replay after an intervening play is a new play, not a duplicate.

## Provider payload and eligibility are different

The provider receives the original play-start timestamp and currently sends artist, track, optional album and optional duration. `chosenByUser`, MBIDs, album artist and track number are not currently included in the scrobble payload. Do not advertise fields as sent just because metadata is locally available.

## Manual qualification still required

The formula and regression logic are covered by repository assertions; that does not prove correctness across real Spotify sessions. Validate pause/resume, seek, track change, same-track repeat, ads, missing sessions, sleep/wake, and multi-day reconciliation against a dedicated Last.fm test profile. Record exact candidate SHA and observed results in the real-world manual matrix; leave unexecuted scenarios `NOT RUN`.
